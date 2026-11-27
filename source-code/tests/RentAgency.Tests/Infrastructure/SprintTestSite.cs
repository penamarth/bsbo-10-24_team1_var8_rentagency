using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RentAgency.Tests.Infrastructure;
using RentAgency.Web.Data;
using RentAgency.Web.Features.Accounts;
using RentAgency.Web.Features.Housing.Application;
using RentAgency.Web.Features.Housing.Domain;

namespace RentAgency.Tests.Infrastructure;

internal sealed class SprintTestSite : IDisposable
{
    private readonly TempDirectory _root = new();
    private readonly List<IServiceScope> _scopes = [];
    private Guid? _moderatorId;
    private string? _moderatorEmail;

    public SprintTestSite(IInterceptor? interceptor = null)
    {
        Factory = new SqliteWebApplicationFactory(
            new Dictionary<string, string?> { ["Storage:RootPath"] = _root.Path },
            interceptor);
    }

    public SqliteWebApplicationFactory Factory { get; }

    public T Get<T>() where T : notnull
    {
        var scope = Factory.Services.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    public async Task<(Guid Id, string Email)> CreateUserAsync(string role, string? email = null)
    {
        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        email ??= $"{Guid.NewGuid():N}@test.local";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            Name = "Тестовый пользователь",
            ContactDetails = "+7 900 000-00-00"
        };

        var created = await users.CreateAsync(user, AuthTestHelper.Password);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(e => e.Description)));

        var roleResult = await users.AddToRoleAsync(user, role);
        Assert.True(roleResult.Succeeded);

        return (user.Id, email);
    }

    public async Task<Guid> ModeratorIdAsync()
    {
        if (_moderatorId is null)
        {
            var moderator = await CreateUserAsync(UserRoles.Moderator);
            _moderatorId = moderator.Id;
            _moderatorEmail = moderator.Email;
        }

        return _moderatorId.Value;
    }

    public string ModeratorEmail => _moderatorEmail
        ?? throw new InvalidOperationException("Moderator not created yet.");

    public static ListingDraft Draft(
        string address = "Тестовая улица, 1",
        decimal price = 50_000m,
        int rooms = 2,
        string? photoKey = null)
    {
        return new ListingDraft(
            address,
            "Светлая квартира рядом с метро.",
            "Длительная аренда, без животных.",
            price,
            rooms,
            45.5,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            [new ListingPhotoInput(photoKey ?? $"listing/{Guid.NewGuid():N}.jpg", "photo.jpg", "image/jpeg")]);
    }

    public async Task<Guid> SubmitListingAsync(Guid ownerId, ListingDraft? draft = null)
    {
        return await Get<IListingService>().SubmitAsync(ownerId, draft ?? Draft());
    }

    public async Task<Guid> PublishListingAsync(Guid ownerId, ListingDraft? draft = null)
    {
        var listings = Get<IListingService>();
        var listingId = await SubmitListingAsync(ownerId, draft);
        var moderatorId = await ModeratorIdAsync();
        var view = await listings.GetForModerationAsync(moderatorId, listingId);
        var outcome = await listings.ModerateAsync(
            moderatorId,
            listingId,
            ModerationResult.Approved,
            null,
            view.Details.Version);

        Assert.True(outcome.Applied);
        return listingId;
    }

    public async Task<int> VersionAsync(Guid listingId)
    {
        return await Get<RentAgencyDbContext>().Listings
            .AsNoTracking()
            .Where(l => l.Id == listingId)
            .Select(l => l.Version)
            .FirstAsync();
    }

    public void Dispose()
    {
        foreach (var scope in _scopes)
        {
            scope.Dispose();
        }

        Factory.Dispose();
        _root.Dispose();
    }
}
