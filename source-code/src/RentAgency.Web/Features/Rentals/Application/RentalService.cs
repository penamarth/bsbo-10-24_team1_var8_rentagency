using Microsoft.EntityFrameworkCore;
using RentAgency.Web.Data;
using RentAgency.Web.Features.Accounts;
using RentAgency.Web.Features.Rentals.Domain;
using RentAgency.Web.Infrastructure;

namespace RentAgency.Web.Features.Rentals.Application;

public sealed record ReviewView(Guid Id, Guid AuthorId, Guid RecipientId, string Text, DateTime CreatedAt);

public interface IRentalService
{
    Task ConfirmCompletionAsync(Guid agreementId, Guid participantId, CancellationToken cancellationToken = default);

    Task<Review> LeaveReviewAsync(
        Guid agreementId,
        Guid authorId,
        Guid recipientId,
        string text,
        CancellationToken cancellationToken = default);

    Task<AgreementView> GetAgreementAsync(Guid agreementId, Guid participantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReviewView>> GetReviewsAsync(Guid agreementId, Guid participantId, CancellationToken cancellationToken = default);
}

public sealed class RentalService(RentAgencyDbContext db, INotificationService notifications) : IRentalService
{
    public async Task ConfirmCompletionAsync(
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

        if (agreement.Status is not (AgreementStatus.Confirmed or AgreementStatus.Completed))
        {
            throw new ConflictException("Аренда ещё не подтверждена.");
        }

        if (agreement.Status == AgreementStatus.Completed)
        {
            return;
        }

        if (agreement.Confirmations.Any(c => c.Type == ConfirmationType.Completion && c.ParticipantId == participantId))
        {
            return;
        }

        var confirmation = new Confirmation
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            ParticipantId = participantId,
            Type = ConfirmationType.Completion,
            ConfirmedAt = DateTime.UtcNow
        };

        agreement.Confirmations.Add(confirmation);
        db.Add(confirmation);

        var completedBy = agreement.Confirmations
            .Where(c => c.Type == ConfirmationType.Completion)
            .Select(c => c.ParticipantId)
            .ToHashSet();

        var bothCompleted = completedBy.Contains(agreement.Request.OwnerId)
            && completedBy.Contains(agreement.Request.TenantId);

        if (bothCompleted)
        {
            agreement.Status = AgreementStatus.Completed;
            agreement.CompletedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);

        await notifications.NotifyAsync(
            agreement.Request.OtherParticipant(participantId),
            completedBy.Contains(participantId)
                ? "Вторая сторона подтвердила завершение аренды."
                : "Сторона подтвердила завершение аренды.",
            cancellationToken);
    }

    public async Task<Review> LeaveReviewAsync(
        Guid agreementId,
        Guid authorId,
        Guid recipientId,
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ValidationException("Напишите отзыв.");
        }

        var agreement = await LoadAgreementAsync(agreementId, cancellationToken)
            ?? throw new NotFoundException("Договорённость не найдена.");

        if (!agreement.Request.IsParticipant(authorId))
        {
            throw new ForbiddenException();
        }

        if (agreement.Status != AgreementStatus.Completed)
        {
            throw new ConflictException("Отзыв доступен после завершения аренды.");
        }

        if (agreement.Request.OtherParticipant(authorId) != recipientId)
        {
            throw new ValidationException("Отзыв можно оставить только о второй стороне.");
        }

        var existing = agreement.Reviews.FirstOrDefault(r => r.AuthorId == authorId);

        if (existing is not null)
        {
            return existing;
        }

        var review = new Review
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            AuthorId = authorId,
            RecipientId = recipientId,
            Text = text.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        db.Reviews.Add(review);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return await db.Reviews
                .AsNoTracking()
                .FirstAsync(r => r.AgreementId == agreementId && r.AuthorId == authorId, cancellationToken);
        }

        await notifications.NotifyAsync(recipientId, "Вам оставлен отзыв об аренде.", cancellationToken);
        return review;
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

        return RequestService.MapAgreement(agreement);
    }

    public async Task<IReadOnlyList<ReviewView>> GetReviewsAsync(
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

        return agreement.Reviews
            .OrderBy(r => r.CreatedAt)
            .Select(r => new ReviewView(r.Id, r.AuthorId, r.RecipientId, r.Text, r.CreatedAt))
            .ToList();
    }

    private Task<RentalAgreement?> LoadAgreementAsync(Guid agreementId, CancellationToken cancellationToken)
    {
        return db.RentalAgreements
            .Include(a => a.Confirmations)
            .Include(a => a.Reviews)
            .Include(a => a.Request)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == agreementId, cancellationToken);
    }
}
