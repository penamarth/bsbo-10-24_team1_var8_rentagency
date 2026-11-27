using Microsoft.EntityFrameworkCore;
using RentAgency.Web.Data;
using RentAgency.Web.Features.Accounts;
using RentAgency.Web.Features.Complaints.Domain;
using RentAgency.Web.Features.Housing.Application;
using RentAgency.Web.Infrastructure;
using RentAgency.Web.Infrastructure.Files;

namespace RentAgency.Web.Features.Complaints.Application;

public sealed class ComplaintService(
    RentAgencyDbContext db,
    IUserDirectory users,
    IListingLookup listings,
    IFileStorage storage,
    INotificationService notifications) : IComplaintService
{
    public async Task<Guid> SubmitAsync(
        Guid reporterId,
        ComplaintTarget target,
        string description,
        IReadOnlyList<UploadInput> evidence,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ValidationException("Опишите проблему.");
        }

        var hasListing = target.ListingId is not null;
        var hasUser = target.TargetUserId is not null;

        if (hasListing == hasUser)
        {
            throw new ValidationException("Укажите ровно одну цель жалобы.");
        }

        if (target.TargetUserId == reporterId)
        {
            throw new ValidationException("Нельзя пожаловаться на себя.");
        }

        if (hasListing && !await listings.ExistsAsync(target.ListingId!.Value, cancellationToken))
        {
            throw new NotFoundException("Объявление не найдено.");
        }

        if (hasUser && !await users.ExistsAsync(target.TargetUserId!.Value, cancellationToken))
        {
            throw new NotFoundException("Пользователь не найден.");
        }

        if (evidence.Count > UploadPolicy.ComplaintEvidenceMaxCount)
        {
            throw new ValidationException(
                $"Можно приложить не более {UploadPolicy.ComplaintEvidenceMaxCount} файлов.");
        }

        if (evidence.Any(e => string.IsNullOrWhiteSpace(e.StorageKey)
            || !e.StorageKey.StartsWith("evidence/", StringComparison.Ordinal)))
        {
            throw new ValidationException("Некорректный файл доказательства.");
        }

        var complaint = new Complaint
        {
            Id = Guid.NewGuid(),
            ReporterId = reporterId,
            TargetListingId = target.ListingId,
            TargetUserId = target.TargetUserId,
            Description = description.Trim(),
            Status = ComplaintStatus.Open,
            SubmittedAt = DateTime.UtcNow
        };

        complaint.Evidence.AddRange(evidence.Select(e => new ComplaintEvidence
        {
            Id = Guid.NewGuid(),
            ComplaintId = complaint.Id,
            StorageKey = e.StorageKey,
            OriginalFileName = e.OriginalFileName,
            ContentType = e.ContentType
        }));

        db.Complaints.Add(complaint);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            db.ChangeTracker.Clear();

            foreach (var item in evidence)
            {
                await storage.DeleteAsync(item.StorageKey, cancellationToken);
            }

            throw;
        }

        return complaint.Id;
    }

    public async Task<ComplaintDetails> GetAsync(
        Guid complaintId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var complaint = await LoadAsync(complaintId, cancellationToken)
            ?? throw new NotFoundException("Жалоба не найдена.");

        await EnsureCanReadAsync(complaint, actorId, cancellationToken);
        return Map(complaint);
    }

    public async Task<IReadOnlyList<ComplaintSummary>> GetMineAsync(
        Guid reporterId,
        CancellationToken cancellationToken = default)
    {
        return await db.Complaints
            .AsNoTracking()
            .Where(c => c.ReporterId == reporterId)
            .OrderByDescending(c => c.SubmittedAt)
            .Select(c => new ComplaintSummary(c.Id, c.ReporterId, c.TargetListingId, c.TargetUserId, c.Status, c.SubmittedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ComplaintSummary>> GetForModeratorAsync(
        Guid moderatorId,
        CancellationToken cancellationToken = default)
    {
        await EnsureModeratorAsync(moderatorId, cancellationToken);

        return await db.Complaints
            .AsNoTracking()
            .OrderBy(c => c.SubmittedAt)
            .Select(c => new ComplaintSummary(c.Id, c.ReporterId, c.TargetListingId, c.TargetUserId, c.Status, c.SubmittedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task RequestDetailsAsync(
        Guid moderatorId,
        Guid complaintId,
        string details,
        CancellationToken cancellationToken = default)
    {
        await EnsureModeratorAsync(moderatorId, cancellationToken);

        if (string.IsNullOrWhiteSpace(details))
        {
            throw new ValidationException("Укажите, какие уточнения нужны.");
        }

        var complaint = await LoadAsync(complaintId, cancellationToken)
            ?? throw new NotFoundException("Жалоба не найдена.");

        if (complaint.Status == ComplaintStatus.Resolved)
        {
            throw new ConflictException("Жалоба уже рассмотрена.");
        }

        var handling = new ComplaintHandling
        {
            Id = Guid.NewGuid(),
            ComplaintId = complaint.Id,
            ModeratorId = moderatorId,
            RequestedDetails = details.Trim(),
            HandledAt = DateTime.UtcNow
        };

        complaint.Handlings.Add(handling);
        db.Add(handling);

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResolveAsync(
        Guid moderatorId,
        Guid complaintId,
        string decision,
        string? rationale,
        CancellationToken cancellationToken = default)
    {
        await EnsureModeratorAsync(moderatorId, cancellationToken);

        if (string.IsNullOrWhiteSpace(decision))
        {
            throw new ValidationException("Укажите решение.");
        }

        var complaint = await LoadAsync(complaintId, cancellationToken)
            ?? throw new NotFoundException("Жалоба не найдена.");

        complaint.Status = ComplaintStatus.Resolved;
        var handling = new ComplaintHandling
        {
            Id = Guid.NewGuid(),
            ComplaintId = complaint.Id,
            ModeratorId = moderatorId,
            Decision = decision.Trim(),
            Rationale = string.IsNullOrWhiteSpace(rationale) ? null : rationale.Trim(),
            HandledAt = DateTime.UtcNow
        };

        complaint.Handlings.Add(handling);
        db.Add(handling);

        await db.SaveChangesAsync(cancellationToken);

        await notifications.NotifyAsync(
            complaint.ReporterId,
            $"Жалоба рассмотрена: {complaint.Status.ToRussian()}.",
            cancellationToken);
    }

    public async Task<Stream> OpenEvidenceAsync(
        Guid complaintId,
        Guid evidenceId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var complaint = await LoadAsync(complaintId, cancellationToken)
            ?? throw new NotFoundException("Жалоба не найдена.");

        await EnsureCanReadAsync(complaint, actorId, cancellationToken);

        var evidence = complaint.Evidence.FirstOrDefault(e => e.Id == evidenceId)
            ?? throw new NotFoundException("Файл не найден.");

        return await storage.OpenReadAsync(evidence.StorageKey, cancellationToken)
            ?? throw new NotFoundException("Файл не найден.");
    }

    private Task<Complaint?> LoadAsync(Guid complaintId, CancellationToken cancellationToken)
    {
        return db.Complaints
            .Include(c => c.Handlings)
            .Include(c => c.Evidence)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == complaintId, cancellationToken);
    }

    private async Task EnsureCanReadAsync(Complaint complaint, Guid actorId, CancellationToken cancellationToken)
    {
        if (complaint.ReporterId == actorId)
        {
            return;
        }

        if (!await users.IsInRoleAsync(actorId, UserRoles.Moderator, cancellationToken))
        {
            throw new ForbiddenException();
        }
    }

    private async Task EnsureModeratorAsync(Guid moderatorId, CancellationToken cancellationToken)
    {
        if (!await users.IsInRoleAsync(moderatorId, UserRoles.Moderator, cancellationToken))
        {
            throw new ForbiddenException();
        }
    }

    private static ComplaintDetails Map(Complaint complaint)
    {
        return new ComplaintDetails(
            complaint.Id,
            complaint.ReporterId,
            complaint.TargetListingId,
            complaint.TargetUserId,
            complaint.Description,
            complaint.Status,
            complaint.SubmittedAt,
            complaint.Evidence
                .Select(e => new ComplaintEvidenceView(e.Id, e.OriginalFileName, e.ContentType))
                .ToList(),
            complaint.Handlings
                .OrderBy(h => h.HandledAt)
                .Select(h => new ComplaintHistoryView(h.ModeratorId, h.RequestedDetails, h.Decision, h.Rationale, h.HandledAt))
                .ToList());
    }
}
