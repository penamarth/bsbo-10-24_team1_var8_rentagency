using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentAgency.Tests.Infrastructure;
using RentAgency.Web.Data;
using RentAgency.Web.Features.Accounts;
using RentAgency.Web.Features.Housing.Application;
using RentAgency.Web.Features.Housing.Domain;
using RentAgency.Web.Infrastructure;
using RentAgency.Web.Infrastructure.Files;

namespace RentAgency.Tests.Housing;

public class ListingServiceTests
{
    [Fact]
    public async Task Submit_CreatesOnModerationListingWithHistory()
    {
        using var site = new SprintTestSite();
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);

        var listingId = await site.SubmitListingAsync(ownerId);

        var db = site.Get<RentAgencyDbContext>();
        var listing = await db.Listings
            .Include(l => l.StatusHistory)
            .Include(l => l.Photos)
            .SingleAsync(l => l.Id == listingId);

        Assert.Equal(ListingStatus.OnModeration, listing.Status);
        Assert.Equal(1, listing.Version);
        Assert.Single(listing.StatusHistory);
        Assert.Single(listing.Photos);
        Assert.Null(listing.PublishedAt);
    }

    [Fact]
    public async Task Submit_NonOwnerIsForbidden()
    {
        using var site = new SprintTestSite();
        var (tenantId, _) = await site.CreateUserAsync(UserRoles.Tenant);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => site.Get<IListingService>().SubmitAsync(tenantId, SprintTestSite.Draft()));
    }

    [Fact]
    public async Task Submit_WithoutPhotosIsRejected()
    {
        using var site = new SprintTestSite();
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);

        var incomplete = SprintTestSite.Draft() with { Photos = [] };

        await Assert.ThrowsAsync<ValidationException>(
            () => site.Get<IListingService>().SubmitAsync(ownerId, incomplete));
    }

    [Fact]
    public async Task Approve_PublishesAndRecordsDecisionAndNotifiesOwner()
    {
        using var site = new SprintTestSite();
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var listingId = await site.SubmitListingAsync(ownerId);
        var moderatorId = await site.ModeratorIdAsync();
        var listings = site.Get<IListingService>();

        var before = await listings.GetForModerationAsync(moderatorId, listingId);
        var outcome = await listings.ModerateAsync(
            moderatorId, listingId, ModerationResult.Approved, "Всё в порядке", before.Details.Version);

        Assert.True(outcome.Applied, $"expected {before.Details.Version}, got {outcome.Version}, {outcome.Status}, {outcome.Message}");
        Assert.Equal(ListingStatus.Published, outcome.Status);

        var db = site.Get<RentAgencyDbContext>();
        var listing = await db.Listings
            .Include(l => l.Decisions)
            .Include(l => l.StatusHistory)
            .SingleAsync(l => l.Id == listingId);

        Assert.Equal(ListingStatus.Published, listing.Status);
        Assert.NotNull(listing.PublishedAt);
        Assert.Single(listing.Decisions);
        Assert.Equal(2, listing.StatusHistory.Count);

        var notifications = await site.Get<INotificationService>().GetForUserAsync(ownerId);
        Assert.Single(notifications);
    }

    [Fact]
    public async Task ReturnForRevision_RequiresReasonAndAllowsResubmission()
    {
        using var site = new SprintTestSite();
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var listingId = await site.SubmitListingAsync(ownerId);
        var moderatorId = await site.ModeratorIdAsync();
        var listings = site.Get<IListingService>();
        var before = await listings.GetForModerationAsync(moderatorId, listingId);

        await Assert.ThrowsAsync<ValidationException>(() => listings.ModerateAsync(
            moderatorId, listingId, ModerationResult.ReturnedForRevision, null, before.Details.Version));

        var outcome = await listings.ModerateAsync(
            moderatorId, listingId, ModerationResult.ReturnedForRevision, "Нет фото кухни", before.Details.Version);
        Assert.True(outcome.Applied);

        var revised = await listings.GetOwnerListingAsync(ownerId, listingId);
        Assert.NotNull(revised);
        Assert.Equal(ListingStatus.NeedsRevision, revised!.Status);
        Assert.Equal("Нет фото кухни", revised.History.Single().Remarks);

        await listings.ResubmitAsync(ownerId, listingId, SprintTestSite.Draft("Исправлено, 2"), revised.Version);

        var after = await listings.GetOwnerListingAsync(ownerId, listingId);
        Assert.Equal(ListingStatus.OnModeration, after!.Status);
        Assert.Equal(3, after.Version);
    }

    [Fact]
    public async Task Resubmit_PublishedListingIsRejected()
    {
        using var site = new SprintTestSite();
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var listingId = await site.PublishListingAsync(ownerId);
        var listings = site.Get<IListingService>();
        var details = await listings.GetOwnerListingAsync(ownerId, listingId);

        await Assert.ThrowsAsync<ConflictException>(() => listings.ResubmitAsync(
            ownerId, listingId, SprintTestSite.Draft(), details!.Version));
    }

    [Fact]
    public async Task StaleModeration_ReturnsCurrentStateWithoutDuplicateHistory()
    {
        using var site = new SprintTestSite();
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var listingId = await site.SubmitListingAsync(ownerId);
        var moderatorId = await site.ModeratorIdAsync();
        var listings = site.Get<IListingService>();

        var first = await listings.ModerateAsync(
            moderatorId, listingId, ModerationResult.Approved, null, await site.VersionAsync(listingId));
        Assert.True(first.Applied);

        var second = await listings.ModerateAsync(
            moderatorId, listingId, ModerationResult.Rejected, "Поздно", 1);

        Assert.False(second.Applied);
        Assert.Equal(ListingStatus.Published, second.Status);

        var db = site.Get<RentAgencyDbContext>();
        Assert.Equal(1, await db.Listings.Where(l => l.Id == listingId).SelectMany(l => l.Decisions).CountAsync());
        Assert.Equal(2, await db.Listings.Where(l => l.Id == listingId).SelectMany(l => l.StatusHistory).CountAsync());
    }

    [Fact]
    public async Task Moderation_IsModeratorOnly()
    {
        using var site = new SprintTestSite();
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var listingId = await site.SubmitListingAsync(ownerId);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => site.Get<IListingService>().GetForModerationAsync(ownerId, listingId));
    }

    [Fact]
    public async Task OwnerListing_IsOwnerOnly()
    {
        using var site = new SprintTestSite();
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var (otherOwnerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var listingId = await site.SubmitListingAsync(ownerId);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => site.Get<IListingService>().GetOwnerListingAsync(otherOwnerId, listingId));
    }
}

public class SearchServiceTests
{
    [Fact]
    public async Task Search_ReturnsOnlyPublishedAndAppliesFilters()
    {
        using var site = new SprintTestSite();
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var listings = site.Get<IListingService>();

        var center = await site.PublishListingAsync(
            ownerId, SprintTestSite.Draft("Тверская улица, 1", 50_000m, 2));
        var outskirts = await site.PublishListingAsync(
            ownerId, SprintTestSite.Draft("Арбат, 2", 30_000m, 1));
        await site.SubmitListingAsync(ownerId, SprintTestSite.Draft("Тверская улица, 3", 10_000m, 1));

        var search = site.Get<ISearchService>();

        var all = await search.SearchAsync(new SearchCriteria(null, null, null));
        Assert.Equal(2, all.Count);

        var byLocation = await search.SearchAsync(new SearchCriteria("Тверская", null, null));
        Assert.Equal(center, Assert.Single(byLocation).ListingId);

        var byPrice = await search.SearchAsync(new SearchCriteria(null, 40_000m, null));
        Assert.Equal(outskirts, Assert.Single(byPrice).ListingId);

        var byRooms = await search.SearchAsync(new SearchCriteria(null, null, 2));
        Assert.Equal(center, Assert.Single(byRooms).ListingId);

        Assert.Null(await search.GetPublishedListingAsync(await site.SubmitListingAsync(
            ownerId, SprintTestSite.Draft("Черёмушки, 4", 20_000m, 1))));
    }
}

public class ListingMediaAuthorizationTests : IDisposable
{
    private readonly TempDirectory _root = new();

    public void Dispose() => _root.Dispose();

    private SqliteWebApplicationFactory CreateFactory() =>
        new(new Dictionary<string, string?> { ["Storage:RootPath"] = _root.Path });

    [Fact]
    public async Task PublishedPhoto_IsAvailablePublicly()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var storage = factory.Services.GetRequiredService<IFileStorage>();
        var stored = await storage.SaveAsync(
            UploadFixtures.AsStream(UploadFixtures.Jpeg()),
            UploadKind.ListingPhoto,
            "image/jpeg",
            "photo.jpg");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RentAgencyDbContext>();
        var housing = new RentAgency.Web.Features.Housing.Domain.Housing
        {
            Id = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Address = "Тестовая, 1",
            Description = "Описание",
            RentalTerms = "Условия"
        };
        var listing = new Listing
        {
            Id = Guid.NewGuid(),
            HousingId = housing.Id,
            Price = 10_000m,
            Rooms = 1,
            Area = 30,
            AvailableFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Status = ListingStatus.Published,
            CreatedAt = DateTime.UtcNow,
            Version = 2,
            PublishedAt = DateTime.UtcNow
        };
        db.Housings.Add(housing);
        db.Listings.Add(listing);
        db.ListingPhotos.Add(new ListingPhoto
        {
            Id = Guid.NewGuid(),
            ListingId = listing.Id,
            StorageKey = stored.StorageKey,
            OriginalFileName = "photo.jpg",
            ContentType = "image/jpeg"
        });
        await db.SaveChangesAsync();

        var response = await client.GetAsync($"/media/listings/{Path.GetFileName(stored.StorageKey)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(UploadFixtures.Jpeg(), await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task UnlinkedOrEvidenceFile_IsNotServed()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var storage = factory.Services.GetRequiredService<IFileStorage>();
        var evidence = await storage.SaveAsync(
            UploadFixtures.AsStream(UploadFixtures.Pdf()),
            UploadKind.ComplaintEvidence,
            "application/pdf",
            "evidence.pdf");

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/media/listings/{Path.GetFileName(evidence.StorageKey)}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/media/listings/{new string('a', 32)}.jpg")).StatusCode);
    }
}
