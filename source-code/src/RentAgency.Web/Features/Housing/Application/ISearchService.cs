using RentAgency.Web.Features.Housing.Domain;

namespace RentAgency.Web.Features.Housing.Application;

public sealed record SearchCriteria(string? Location, decimal? MaxPrice, int? Rooms);

public interface ISearchService
{
    Task<IReadOnlyList<ListingCard>> SearchAsync(SearchCriteria criteria, CancellationToken cancellationToken = default);

    Task<OwnerListingDetails?> GetPublishedListingAsync(Guid listingId, CancellationToken cancellationToken = default);
}
