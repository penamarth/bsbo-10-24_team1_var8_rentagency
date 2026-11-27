using Microsoft.EntityFrameworkCore;
using RentAgency.Web.Data;
using RentAgency.Web.Features.Accounts;
using RentAgency.Web.Features.Housing.Application;
using RentAgency.Web.Features.Housing.Domain;
using RentAgency.Web.Infrastructure;
using HousingRoot = RentAgency.Web.Features.Housing.Domain.Housing;

namespace RentAgency.Web.Features.Housing.Application;

public sealed class ListingService(
    RentAgencyDbContext db,
    IUserDirectory userDirectory,
    INotificationService notifications) : IListingService
{
    public async Task<bool> ExistsAsync(Guid listingId, CancellationToken cancellationToken = default)
    {
        return await db.Listings.AnyAsync(l => l.Id == listingId, cancellationToken);
    }

    public async Task<Guid> SubmitAsync(Guid ownerId, ListingDraft draft, CancellationToken cancellationToken = default)
    {
        if (!await userDirectory.IsInRoleAsync(ownerId, UserRoles.Owner, cancellationToken))
        {
            throw new ForbiddenException("Размещать объявления может только арендодатель.");
        }

        ValidateDraft(draft);

        var now = DateTime.UtcNow;
        var housing = new HousingRoot
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Address = draft.Address.Trim(),
            Description = draft.Description.Trim(),
            RentalTerms = draft.RentalTerms.Trim()
        };

        var listing = new Listing
        {
            Id = Guid.NewGuid(),
            HousingId = housing.Id,
            Price = draft.Price,
            Rooms = draft.Rooms,
            Area = draft.Area,
            AvailableFrom = draft.AvailableFrom,
            Status = ListingStatus.OnModeration,
            CreatedAt = now,
            Version = 0
        };

        listing.ChangeStatus(ListingStatus.OnModeration, now);
        listing.Photos.AddRange(MapPhotos(draft, listing.Id));
        housing.Listings.Add(listing);
        db.Housings.Add(housing);

        await db.SaveChangesAsync(cancellationToken);
        return listing.Id;
    }

    public async Task ResubmitAsync(
        Guid ownerId,
        Guid listingId,
        ListingDraft draft,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ValidateDraft(draft);

        var listing = await LoadListingAsync(listingId, cancellationToken)
            ?? throw new NotFoundException("Объявление не найдено.");

        if (listing.Housing.OwnerId != ownerId)
        {
            throw new ForbiddenException();
        }

        if (listing.Status != ListingStatus.NeedsRevision)
        {
            throw new ConflictException($"Объявление в статусе «{listing.Status.ToRussian()}» нельзя отправить повторно.");
        }

        if (listing.Version != expectedVersion)
        {
            throw new ConflictException("Объявление уже изменилось. Обновите страницу и повторите.");
        }

        listing.Housing.Address = draft.Address.Trim();
        listing.Housing.Description = draft.Description.Trim();
        listing.Housing.RentalTerms = draft.RentalTerms.Trim();
        listing.Price = draft.Price;
        listing.Rooms = draft.Rooms;
        listing.Area = draft.Area;
        listing.AvailableFrom = draft.AvailableFrom;

        listing.Photos.Clear();
        var photos = MapPhotos(draft, listing.Id).ToList();
        listing.Photos.AddRange(photos);
        db.AddRange(photos);
        db.Add(listing.ChangeStatus(ListingStatus.OnModeration, DateTime.UtcNow));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Объявление уже изменилось. Обновите страницу и повторите.");
        }
    }

    public async Task<OwnerListingDetails?> GetOwnerListingAsync(
        Guid ownerId,
        Guid listingId,
        CancellationToken cancellationToken = default)
    {
        var listing = await LoadListingAsync(listingId, cancellationToken);

        if (listing is null)
        {
            return null;
        }

        if (listing.Housing.OwnerId != ownerId)
        {
            throw new ForbiddenException();
        }

        return MapDetails(listing);
    }

    public async Task<IReadOnlyList<OwnerListingDetails>> GetOwnerListingsAsync(
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        var listings = await QueryListings()
            .Where(l => l.Housing.OwnerId == ownerId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(cancellationToken);

        return listings.Select(MapDetails).ToList();
    }

    public async Task<IReadOnlyList<ModerationQueueItem>> GetModerationQueueAsync(
        Guid moderatorId,
        CancellationToken cancellationToken = default)
    {
        await EnsureModeratorAsync(moderatorId, cancellationToken);

        return await db.Listings
            .AsNoTracking()
            .Where(l => l.Status == ListingStatus.OnModeration)
            .OrderBy(l => l.CreatedAt)
            .Select(l => new ModerationQueueItem(l.Id, l.Housing.Address, l.Price, l.CreatedAt, l.Version))
            .ToListAsync(cancellationToken);
    }

    public async Task<ModerationView> GetForModerationAsync(
        Guid moderatorId,
        Guid listingId,
        CancellationToken cancellationToken = default)
    {
        await EnsureModeratorAsync(moderatorId, cancellationToken);

        var listing = await LoadListingAsync(listingId, cancellationToken)
            ?? throw new NotFoundException("Объявление не найдено.");

        return new ModerationView(MapDetails(listing), listing.Housing.OwnerId);
    }

    public async Task<ModerationOutcome> ModerateAsync(
        Guid moderatorId,
        Guid listingId,
        ModerationResult result,
        string? remarks,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        await EnsureModeratorAsync(moderatorId, cancellationToken);

        if (result != ModerationResult.Approved && string.IsNullOrWhiteSpace(remarks))
        {
            throw new ValidationException("Укажите причину возврата или отклонения.");
        }

        var listing = await LoadListingAsync(listingId, cancellationToken)
            ?? throw new NotFoundException("Объявление не найдено.");

        if (listing.Version != expectedVersion)
        {
            return new ModerationOutcome(
                false,
                listing.Status,
                listing.Version,
                $"Объявление уже обработано. Текущий статус: «{listing.Status.ToRussian()}».");
        }

        var now = DateTime.UtcNow;
        var newStatus = result switch
        {
            ModerationResult.Approved => ListingStatus.Published,
            ModerationResult.ReturnedForRevision => ListingStatus.NeedsRevision,
            ModerationResult.Rejected => ListingStatus.Rejected,
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, null)
        };

        db.Add(listing.ChangeStatus(newStatus, now));
        var decision = new ModerationDecision
        {
            Id = Guid.NewGuid(),
            ListingId = listing.Id,
            ModeratorId = moderatorId,
            Result = result,
            Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim(),
            DecidedAt = now,
            ListingVersion = listing.Version
        };
        listing.Decisions.Add(decision);
        db.Add(decision);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            db.ChangeTracker.Clear();
            var current = await db.Listings
                .AsNoTracking()
                .Where(l => l.Id == listingId)
                .Select(l => new { l.Status, l.Version })
                .FirstAsync(cancellationToken);

            return new ModerationOutcome(
                false,
                current.Status,
                current.Version,
                $"Объявление уже обработано. Текущий статус: «{current.Status.ToRussian()}».");
        }

        await notifications.NotifyAsync(
            listing.Housing.OwnerId,
            $"Статус объявления: «{listing.Status.ToRussian()}».",
            cancellationToken);

        return new ModerationOutcome(
            true,
            listing.Status,
            listing.Version,
            $"Статус объявления: «{listing.Status.ToRussian()}».");
    }

    public async Task<bool> MarkRentedAsync(
        Guid listingId,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var listing = await LoadListingAsync(listingId, cancellationToken);

        if (listing is null || listing.Status != ListingStatus.Published || listing.Version != expectedVersion)
        {
            return false;
        }

        db.Add(listing.ChangeStatus(ListingStatus.Rented, DateTime.UtcNow));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    private IQueryable<Listing> QueryListings()
    {
        return db.Listings
            .Include(l => l.Housing)
            .Include(l => l.Photos)
            .Include(l => l.Decisions)
            .AsSplitQuery();
    }

    private Task<Listing?> LoadListingAsync(Guid listingId, CancellationToken cancellationToken)
    {
        return QueryListings().FirstOrDefaultAsync(l => l.Id == listingId, cancellationToken);
    }

    private async Task EnsureModeratorAsync(Guid moderatorId, CancellationToken cancellationToken)
    {
        if (!await userDirectory.IsInRoleAsync(moderatorId, UserRoles.Moderator, cancellationToken))
        {
            throw new ForbiddenException();
        }
    }

    private static IEnumerable<ListingPhoto> MapPhotos(ListingDraft draft, Guid listingId)
    {
        return draft.Photos.Select((photo, index) => new ListingPhoto
        {
            Id = Guid.NewGuid(),
            ListingId = listingId,
            StorageKey = photo.StorageKey,
            OriginalFileName = photo.OriginalFileName,
            ContentType = photo.ContentType,
            SortOrder = index
        });
    }

    private static OwnerListingDetails MapDetails(Listing listing)
    {
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

    private static void ValidateDraft(ListingDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Address))
        {
            throw new ValidationException("Укажите адрес.");
        }

        if (string.IsNullOrWhiteSpace(draft.Description))
        {
            throw new ValidationException("Добавьте описание.");
        }

        if (string.IsNullOrWhiteSpace(draft.RentalTerms))
        {
            throw new ValidationException("Укажите условия сдачи.");
        }

        if (draft.Price <= 0)
        {
            throw new ValidationException("Цена должна быть больше нуля.");
        }

        if (draft.Rooms <= 0)
        {
            throw new ValidationException("Укажите количество комнат.");
        }

        if (draft.Area <= 0)
        {
            throw new ValidationException("Укажите площадь.");
        }

        if (draft.Photos.Count is 0 or > Infrastructure.Files.UploadPolicy.ListingPhotoMaxCount)
        {
            throw new ValidationException(
                $"Нужно от 1 до {Infrastructure.Files.UploadPolicy.ListingPhotoMaxCount} фотографий.");
        }

        if (draft.Photos.Any(p => string.IsNullOrWhiteSpace(p.StorageKey) || !p.StorageKey.StartsWith("listing/", StringComparison.Ordinal)))
        {
            throw new ValidationException("Некорректный файл фотографии.");
        }
    }
}
