using RentAgency.Web.Features.Complaints.Domain;

namespace RentAgency.Web.Features.Complaints.Application;

public sealed record ComplaintTarget(Guid? ListingId, Guid? TargetUserId);

public sealed record UploadInput(string StorageKey, string OriginalFileName, string ContentType);

public sealed record ComplaintEvidenceView(Guid Id, string OriginalFileName, string ContentType);

public sealed record ComplaintHistoryView(
    Guid ModeratorId,
    string? RequestedDetails,
    string? Decision,
    string? Rationale,
    DateTime HandledAt);

public sealed record ComplaintDetails(
    Guid Id,
    Guid ReporterId,
    Guid? TargetListingId,
    Guid? TargetUserId,
    string Description,
    ComplaintStatus Status,
    DateTime SubmittedAt,
    IReadOnlyList<ComplaintEvidenceView> Evidence,
    IReadOnlyList<ComplaintHistoryView> History);

public sealed record ComplaintSummary(
    Guid Id,
    Guid ReporterId,
    Guid? TargetListingId,
    Guid? TargetUserId,
    ComplaintStatus Status,
    DateTime SubmittedAt);

public interface IComplaintService
{
    Task<Guid> SubmitAsync(
        Guid reporterId,
        ComplaintTarget target,
        string description,
        IReadOnlyList<UploadInput> evidence,
        CancellationToken cancellationToken = default);

    Task<ComplaintDetails> GetAsync(Guid complaintId, Guid actorId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ComplaintSummary>> GetMineAsync(Guid reporterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ComplaintSummary>> GetForModeratorAsync(Guid moderatorId, CancellationToken cancellationToken = default);

    Task RequestDetailsAsync(
        Guid moderatorId,
        Guid complaintId,
        string details,
        CancellationToken cancellationToken = default);

    Task ResolveAsync(
        Guid moderatorId,
        Guid complaintId,
        string decision,
        string? rationale,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenEvidenceAsync(
        Guid complaintId,
        Guid evidenceId,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
