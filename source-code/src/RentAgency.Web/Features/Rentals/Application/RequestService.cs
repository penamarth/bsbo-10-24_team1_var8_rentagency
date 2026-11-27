using Microsoft.EntityFrameworkCore;
using RentAgency.Web.Data;
using RentAgency.Web.Features.Accounts;
using RentAgency.Web.Features.Housing.Application;
using RentAgency.Web.Features.Housing.Domain;
using RentAgency.Web.Features.Rentals.Domain;
using RentAgency.Web.Infrastructure;

namespace RentAgency.Web.Features.Rentals.Application;

public sealed class RequestService(
    RentAgencyDbContext db,
    IListingLookup listingLookup,
    IListingService listingService,
    INotificationService notifications) : IRequestService
{
    public async Task<Guid> SubmitAsync(
        Guid tenantId,
        Guid listingId,
        CreateRentalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.InitialMessage))
        {
            throw new ValidationException("Напишите сообщение.");
        }

        if (request.ProposedEnd <= request.ProposedStart)
        {
            throw new ValidationException("Дата окончания должна быть позже даты начала.");
        }

        var listing = await db.Listings
            .AsNoTracking()
            .Where(l => l.Id == listingId)
            .Select(l => new { l.Status, l.Housing.OwnerId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Объявление не найдено.");

        if (listing.OwnerId == tenantId)
        {
            throw new ValidationException("Нельзя откликнуться на своё объявление.");
        }

        if (listing.Status != ListingStatus.Published)
        {
            throw new ConflictException("Объявление недоступно для отклика.");
        }

        var duplicate = await db.RentalRequests.AnyAsync(
            r => r.ListingId == listingId
                && r.TenantId == tenantId
                && (r.Status == RequestStatus.New || r.Status == RequestStatus.InDiscussion),
            cancellationToken);

        if (duplicate)
        {
            throw new ConflictException("У вас уже есть активная заявка на это объявление.");
        }

        var entity = new RentalRequest
        {
            Id = Guid.NewGuid(),
            ListingId = listingId,
            OwnerId = listing.OwnerId,
            TenantId = tenantId,
            InitialMessage = request.InitialMessage.Trim(),
            ProposedStart = request.ProposedStart,
            ProposedEnd = request.ProposedEnd,
            Status = RequestStatus.New,
            CreatedAt = DateTime.UtcNow
        };

        db.RentalRequests.Add(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            throw new ConflictException("Объявление недоступно для отклика.");
        }

        await notifications.NotifyAsync(
            listing.OwnerId,
            $"Новая заявка на объявление по адресу {await AddressAsync(listingId, cancellationToken)}.",
            cancellationToken);

        return entity.Id;
    }

    public async Task<RentalRequestView> GetRequestAsync(
        Guid requestId,
        Guid participantId,
        CancellationToken cancellationToken = default)
    {
        var request = await LoadRequestAsync(requestId, cancellationToken)
            ?? throw new NotFoundException("Заявка не найдена.");

        if (!request.IsParticipant(participantId))
        {
            throw new ForbiddenException();
        }

        return MapView(request);
    }

    public async Task<IReadOnlyList<OwnerRequestSummary>> GetOwnerRequestsAsync(
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        return await db.RentalRequests
            .AsNoTracking()
            .Where(r => r.OwnerId == ownerId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new OwnerRequestSummary(
                r.Id,
                r.ListingId,
                db.Listings.Where(l => l.Id == r.ListingId).Select(l => l.Housing.Address).FirstOrDefault() ?? "—",
                r.TenantId,
                r.Status,
                r.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OwnerRequestSummary>> GetTenantRequestsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await db.RentalRequests
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new OwnerRequestSummary(
                r.Id,
                r.ListingId,
                db.Listings.Where(l => l.Id == r.ListingId).Select(l => l.Housing.Address).FirstOrDefault() ?? "—",
                r.TenantId,
                r.Status,
                r.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task StartDiscussionAsync(
        Guid requestId,
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        var request = await LoadRequestAsync(requestId, cancellationToken)
            ?? throw new NotFoundException("Заявка не найдена.");

        if (request.OwnerId != ownerId)
        {
            throw new ForbiddenException();
        }

        if (request.Status != RequestStatus.New)
        {
            throw new ConflictException($"Заявка в статусе «{request.Status.ToRussian()}».");
        }

        request.Status = RequestStatus.InDiscussion;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectAsync(
        Guid requestId,
        Guid ownerId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationException("Укажите причину отклонения.");
        }

        var request = await LoadRequestAsync(requestId, cancellationToken)
            ?? throw new NotFoundException("Заявка не найдена.");

        if (request.OwnerId != ownerId)
        {
            throw new ForbiddenException();
        }

        if (request.Status is not (RequestStatus.New or RequestStatus.InDiscussion))
        {
            throw new ConflictException($"Заявка в статусе «{request.Status.ToRussian()}».");
        }

        request.Status = RequestStatus.Rejected;
        request.RejectionReason = reason.Trim();
        await db.SaveChangesAsync(cancellationToken);

        await notifications.NotifyAsync(
            request.TenantId,
            "Ваша заявка отклонена. Причина указана в заявке.",
            cancellationToken);
    }

    public async Task SendMessageAsync(
        Guid requestId,
        Guid authorId,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ValidationException("Введите сообщение.");
        }

        var request = await LoadRequestAsync(requestId, cancellationToken)
            ?? throw new NotFoundException("Заявка не найдена.");

        if (!request.IsParticipant(authorId))
        {
            throw new ForbiddenException();
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
            RequestId = request.Id,
            AuthorId = authorId,
            Text = text.Trim(),
            SentAt = DateTime.UtcNow
        };

        request.Messages.Add(message);
        db.Add(message);

        await db.SaveChangesAsync(cancellationToken);

        await notifications.NotifyAsync(
            request.OtherParticipant(authorId),
            "Новое сообщение в переписке по заявке.",
            cancellationToken);
    }

    public async Task<Guid> ProposeAgreementAsync(
        Guid requestId,
        Guid participantId,
        RentalTerms terms,
        CancellationToken cancellationToken = default)
    {
        ValidateTerms(terms);

        var request = await LoadRequestAsync(requestId, cancellationToken)
            ?? throw new NotFoundException("Заявка не найдена.");

        if (!request.IsParticipant(participantId))
        {
            throw new ForbiddenException();
        }

        if (request.Status is RequestStatus.Rejected or RequestStatus.Rented)
        {
            throw new ConflictException($"Заявка в статусе «{request.Status.ToRussian()}».");
        }

        var agreement = request.Agreement;

        if (agreement is null)
        {
            agreement = new RentalAgreement
            {
                Id = Guid.NewGuid(),
                RequestId = request.Id,
                Status = AgreementStatus.Draft,
                Terms = terms,
                TermsVersion = 1,
                CreatedAt = DateTime.UtcNow
            };

            db.RentalAgreements.Add(agreement);
        }
        else
        {
            if (agreement.Status != AgreementStatus.Draft)
            {
                throw new ConflictException("Условия уже согласованы.");
            }

            agreement.Terms = terms;
            agreement.TermsVersion++;
        }

        await db.SaveChangesAsync(cancellationToken);

        await notifications.NotifyAsync(
            request.OtherParticipant(participantId),
            "Предложены условия аренды.",
            cancellationToken);

        return agreement.Id;
    }

    public async Task ConfirmAgreementAsync(
        Guid agreementId,
        Guid participantId,
        int expectedTermsVersion,
        CancellationToken cancellationToken = default)
    {
        var agreement = await LoadAgreementAsync(agreementId, cancellationToken)
            ?? throw new NotFoundException("Договорённость не найдена.");

        if (!agreement.Request.IsParticipant(participantId))
        {
            throw new ForbiddenException();
        }

        if (agreement.Status != AgreementStatus.Draft)
        {
            throw new ConflictException("Условия уже согласованы.");
        }

        if (agreement.TermsVersion != expectedTermsVersion)
        {
            throw new ConflictException("Условия изменились. Проверьте новую версию.");
        }

        var alreadyConfirmed = agreement.Confirmations.Any(
            c => c.Type == ConfirmationType.Terms
                && c.TermsVersion == expectedTermsVersion
                && c.ParticipantId == participantId);

        if (alreadyConfirmed)
        {
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var confirmation = new Confirmation
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            ParticipantId = participantId,
            Type = ConfirmationType.Terms,
            TermsVersion = expectedTermsVersion,
            ConfirmedAt = DateTime.UtcNow
        };

        agreement.Confirmations.Add(confirmation);
        db.Add(confirmation);

        var confirmedBy = agreement.Confirmations
            .Where(c => c.Type == ConfirmationType.Terms && c.TermsVersion == expectedTermsVersion)
            .Select(c => c.ParticipantId)
            .ToHashSet();

        var bothConfirmed = confirmedBy.Contains(agreement.Request.OwnerId)
            && confirmedBy.Contains(agreement.Request.TenantId);

        if (bothConfirmed)
        {
            var listingVersion = await db.Listings
                .AsNoTracking()
                .Where(l => l.Id == agreement.Request.ListingId)
                .Select(l => l.Version)
                .FirstAsync(cancellationToken);

            var rented = await listingService.MarkRentedAsync(
                agreement.Request.ListingId,
                listingVersion,
                cancellationToken);

            if (!rented)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new ConflictException("Объявление уже недоступно.");
            }

            agreement.Status = AgreementStatus.Confirmed;
            agreement.ConfirmedAt = DateTime.UtcNow;
            agreement.Request.Status = RequestStatus.Rented;

            var otherRequests = await db.RentalRequests
                .Where(r => r.ListingId == agreement.Request.ListingId
                    && r.Id != agreement.RequestId
                    && (r.Status == RequestStatus.New || r.Status == RequestStatus.InDiscussion))
                .ToListAsync(cancellationToken);

            foreach (var other in otherRequests)
            {
                other.Status = RequestStatus.Rejected;
                other.RejectionReason = "Жильё уже сдано.";
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (bothConfirmed)
        {
            await notifications.NotifyAsync(
                agreement.Request.TenantId,
                "Условия согласованы обеими сторонами.",
                cancellationToken);

            await notifications.NotifyAsync(
                agreement.Request.OwnerId,
                "Условия согласованы обеими сторонами.",
                cancellationToken);
        }
    }

    public async Task<AgreementView> GetAgreementAsync(
        Guid agreementId,
        Guid participantId,
        CancellationToken cancellationToken = default)
    {
        var agreement = await LoadAgreementAsync(agreementId, cancellationToken)
            ?? throw new NotFoundException("Договорённость не найдена.");

        if (!agreement.Request.IsParticipant(participantId))
        {
            throw new ForbiddenException();
        }

        return MapAgreement(agreement);
    }

    private IQueryable<RentalRequest> QueryRequests()
    {
        return db.RentalRequests
            .Include(r => r.Messages)
            .Include(r => r.Agreement!)
            .ThenInclude(a => a.Confirmations)
            .AsSplitQuery();
    }

    private Task<RentalRequest?> LoadRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        return QueryRequests().FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
    }

    private Task<RentalAgreement?> LoadAgreementAsync(Guid agreementId, CancellationToken cancellationToken)
    {
        return db.RentalAgreements
            .Include(a => a.Confirmations)
            .Include(a => a.Request)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == agreementId, cancellationToken);
    }

    private async Task<string> AddressAsync(Guid listingId, CancellationToken cancellationToken)
    {
        return await db.Listings
            .AsNoTracking()
            .Where(l => l.Id == listingId)
            .Select(l => l.Housing.Address)
            .FirstOrDefaultAsync(cancellationToken) ?? "—";
    }

    private static void ValidateTerms(RentalTerms terms)
    {
        if (terms.End <= terms.Start)
        {
            throw new ValidationException("Дата окончания должна быть позже даты начала.");
        }

        if (terms.MonthlyPrice <= 0)
        {
            throw new ValidationException("Укажите цену аренды.");
        }

        if (terms.Deposit < 0)
        {
            throw new ValidationException("Залог не может быть отрицательным.");
        }
    }

    private static RentalRequestView MapView(RentalRequest request)
    {
        return new RentalRequestView(
            request.Id,
            request.ListingId,
            request.OwnerId,
            request.TenantId,
            request.InitialMessage,
            request.ProposedStart,
            request.ProposedEnd,
            request.Status,
            request.RejectionReason,
            request.CreatedAt,
            request.Messages
                .OrderBy(m => m.SentAt)
                .Select(m => new RequestMessageView(m.Id, m.AuthorId, m.Text, m.SentAt))
                .ToList(),
            request.Agreement is null ? null : MapAgreement(request.Agreement));
    }

    internal static AgreementView MapAgreement(RentalAgreement agreement)
    {
        return new AgreementView(
            agreement.Id,
            agreement.Status,
            agreement.Terms,
            agreement.TermsVersion,
            agreement.Confirmations
                .Where(c => c.Type == ConfirmationType.Terms && c.TermsVersion == agreement.TermsVersion)
                .Select(c => c.ParticipantId)
                .Distinct()
                .ToList(),
            agreement.Confirmations
                .Where(c => c.Type == ConfirmationType.Completion)
                .Select(c => c.ParticipantId)
                .Distinct()
                .ToList(),
            agreement.ConfirmedAt,
            agreement.CompletedAt);
    }
}
