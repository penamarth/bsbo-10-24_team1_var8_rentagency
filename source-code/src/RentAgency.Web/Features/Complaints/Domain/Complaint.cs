namespace RentAgency.Web.Features.Complaints.Domain;

public enum ComplaintStatus
{
    Open,
    AwaitingReporter,
    Resolved
}

public static class ComplaintStatusText
{
    public static string ToRussian(this ComplaintStatus status)
    {
        return status switch
        {
            ComplaintStatus.Open => "Открыта",
            ComplaintStatus.AwaitingReporter => "Ожидает уточнений",
            ComplaintStatus.Resolved => "Рассмотрена",
            _ => status.ToString()
        };
    }
}

public class Complaint
{
    public Guid Id { get; set; }

    public Guid ReporterId { get; set; }

    public Guid? TargetListingId { get; set; }

    public Guid? TargetUserId { get; set; }

    public string Description { get; set; } = string.Empty;

    public ComplaintStatus Status { get; set; }

    public DateTime SubmittedAt { get; set; }

    public List<ComplaintHandling> Handlings { get; set; } = [];

    public List<ComplaintEvidence> Evidence { get; set; } = [];
}

public class ComplaintHandling
{
    public Guid Id { get; set; }

    public Guid ComplaintId { get; set; }

    public Guid ModeratorId { get; set; }

    public string? RequestedDetails { get; set; }

    public string? Decision { get; set; }

    public string? Rationale { get; set; }

    public DateTime HandledAt { get; set; }
}

public class ComplaintEvidence
{
    public Guid Id { get; set; }

    public Guid ComplaintId { get; set; }

    public string StorageKey { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
}
