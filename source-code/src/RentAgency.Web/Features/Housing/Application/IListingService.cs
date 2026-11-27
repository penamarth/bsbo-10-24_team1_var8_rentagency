using RentAgency.Web.Features.Housing.Domain;

namespace RentAgency.Web.Features.Housing.Application;

public sealed record ListingCard(
    Guid ListingId,
    string Address,
    decimal Price,
    int Rooms,
    double Area,
    DateOnly AvailableFrom,
    string? CoverPhotoKey);

public sealed record ModerationHistoryItem(
    ModerationResult Result,
    string? Remarks,
    DateTime DecidedAt,
    Guid ModeratorId);

public sealed record OwnerListingDetails(
    Guid ListingId,
    Guid OwnerId,
    string Address,
    string Description,
    string RentalTerms,
    decimal Price,
    int Rooms,
    double Area,
    DateOnly AvailableFrom,
    ListingStatus Status,
    int Version,
    IReadOnlyList<string> PhotoKeys,
    IReadOnlyList<ModerationHistoryItem> History);

public sealed record ModerationQueueItem(
    Guid ListingId,
    string Address,
    decimal Price,
    DateTime CreatedAt,
    int Version);

public sealed record ModerationView(
    OwnerListingDetails Details,
    Guid OwnerId);

public sealed record ModerationOutcome(
    bool Applied,
    ListingStatus Status,
    int Version,
    string Message);

public interface IListingLookup
{
    Task<bool> ExistsAsync(Guid listingId, CancellationToken cancellationToken = default);
}

public interface IListingService : IListingLookup
{
    Task<Guid> SubmitAsync(Guid ownerId, ListingDraft draft, CancellationToken cancellationToken = default);

    Task ResubmitAsync(Guid ownerId, Guid listingId, ListingDraft draft, int expectedVersion, CancellationToken cancellationToken = default);

    Task<OwnerListingDetails?> GetOwnerListingAsync(Guid ownerId, Guid listingId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OwnerListingDetails>> GetOwnerListingsAsync(Guid ownerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModerationQueueItem>> GetModerationQueueAsync(Guid moderatorId, CancellationToken cancellationToken = default);

    Task<ModerationView> GetForModerationAsync(Guid moderatorId, Guid listingId, CancellationToken cancellationToken = default);

    Task<ModerationOutcome> ModerateAsync(
        Guid moderatorId,
        Guid listingId,
        ModerationResult result,
        string? remarks,
        int expectedVersion,
        CancellationToken cancellationToken = default);

    Task<bool> MarkRentedAsync(Guid listingId, int expectedVersion, CancellationToken cancellationToken = default);
}
