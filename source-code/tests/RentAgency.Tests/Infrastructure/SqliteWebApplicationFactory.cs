using System.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RentAgency.Web.Data;

namespace RentAgency.Tests.Infrastructure;

public class SqliteWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly Dictionary<string, string?>? _configuration;
    private readonly IInterceptor? _interceptor;

    public SqliteWebApplicationFactory(
        Dictionary<string, string?>? configuration = null,
        IInterceptor? interceptor = null)
    {
        _configuration = configuration;
        _interceptor = interceptor;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
        }

        if (_configuration is not null)
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(_configuration));
        }

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<RentAgencyDbContext>>();
            services.RemoveAll<RentAgencyDbContext>();
            services.RemoveAll<IDbContextOptionsConfiguration<RentAgencyDbContext>>();

            services.AddDbContext<RentAgencyDbContext>(options =>
            {
                options.UseSqlite(_connection);

                if (_interceptor is not null)
                {
                    options.AddInterceptors(_interceptor);
                }
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}

public sealed class FailNextSaveInterceptor : SaveChangesInterceptor
{
    private int _remainingFailures;

    public FailNextSaveInterceptor(int failures = 0)
    {
        _remainingFailures = failures;
    }

    public bool FailNext { get; set; }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (FailNext)
        {
            FailNext = false;
            throw new InvalidOperationException("Simulated database failure.");
        }

        if (_remainingFailures > 0)
        {
            _remainingFailures--;
            throw new InvalidOperationException("Simulated database failure.");
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
