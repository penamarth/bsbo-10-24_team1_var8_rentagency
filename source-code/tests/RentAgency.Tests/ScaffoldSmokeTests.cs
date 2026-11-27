using Microsoft.Extensions.DependencyInjection;
using RentAgency.Tests.Infrastructure;
using RentAgency.Web.Data;

namespace RentAgency.Tests;

public class ScaffoldSmokeTests
{
    [Fact]
    public async Task HomeRoute_ReturnsOkAndInitializesSqliteSchema()
    {
        using var factory = new SqliteWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RentAgencyDbContext>();

        Assert.True(db.Database.CanConnect());
        Assert.NotNull(db.Users);
    }
}
