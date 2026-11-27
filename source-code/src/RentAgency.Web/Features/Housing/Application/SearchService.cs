using Microsoft.EntityFrameworkCore;
using RentAgency.Web.Data;
using RentAgency.Web.Features.Housing.Domain;

namespace RentAgency.Web.Features.Housing.Application;

public sealed class SearchService(RentAgencyDbContext db) : ISearchService
{
    public async Task<IReadOnlyList<ListingCard>> SearchAsync(
        SearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = db.Listings
            .AsNoTracking()
            .Where(l => l.Status == ListingStatus.Published);

        if (!string.IsNullOrWhiteSpace(criteria.Location))
        {
            var location = criteria.Location.Trim();
            query = query.Where(l => EF.Functions.Like(l.Housing.Address, $"%{location}%"));
        }

        if (criteria.MaxPrice is not null)
        {
            query = query.Where(l => l.Price <= criteria.MaxPrice);
        }

        if (criteria.Rooms is not null)
        {
            query = query.Where(l => l.Rooms == criteria.Rooms);
        }

        return await query
            .OrderByDescending(l => l.PublishedAt)
            .Select(l => new ListingCard(
                l.Id,
                l.Housing.Address,
                l.Price,
                l.Rooms,
                l.Area,
                l.AvailableFrom,
                l.Photos.OrderBy(p => p.SortOrder).Select(p => p.StorageKey).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task<OwnerListingDetails?> GetPublishedListingAsync(
        Guid listingId,
        CancellationToken cancellationToken = default)
    {
        var listing = await db.Listings
            .AsNoTracking()
            .Include(l => l.Housing)
            .Include(l => l.Photos)
            .Include(l => l.Decisions)
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                l => l.Id == listingId && l.Status == ListingStatus.Published,
                cancellationToken);

        if (listing is null)
        {
            return null;
        }

        return new OwnerListingDetails(
            listing.Id,
            listing.Housing.OwnerId,
            listing.Housing.Address,
            listing.Housing.Description,
            listing.Housing.RentalTerms,
            listing.Price,
            listing.Rooms,
            listing.Area,
            listing.AvailableFrom,
            listing.Status,
            listing.Version,
            listing.Photos.OrderBy(p => p.SortOrder).Select(p => p.StorageKey).ToList(),
            listing.Decisions
                .OrderByDescending(d => d.DecidedAt)
                .Select(d => new ModerationHistoryItem(d.Result, d.Remarks, d.DecidedAt, d.ModeratorId))
                .ToList());
    }
}
