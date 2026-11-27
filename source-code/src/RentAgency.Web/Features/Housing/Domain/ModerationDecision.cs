namespace RentAgency.Web.Features.Housing.Domain;

public class ModerationDecision
{
    public Guid Id { get; set; }

    public Guid ListingId { get; set; }

    public Guid ModeratorId { get; set; }

    public ModerationResult Result { get; set; }

    public string? Remarks { get; set; }

    public DateTime DecidedAt { get; set; }

    public int ListingVersion { get; set; }
}
