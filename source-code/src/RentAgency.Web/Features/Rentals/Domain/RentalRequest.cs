namespace RentAgency.Web.Features.Rentals.Domain;

public class RentalRequest
{
    public Guid Id { get; set; }

    public Guid ListingId { get; set; }

    public Guid OwnerId { get; set; }

    public Guid TenantId { get; set; }

    public string InitialMessage { get; set; } = string.Empty;

    public DateOnly ProposedStart { get; set; }

    public DateOnly ProposedEnd { get; set; }

    public RequestStatus Status { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<Message> Messages { get; set; } = [];

    public RentalAgreement? Agreement { get; set; }

    public bool IsParticipant(Guid userId) => userId == OwnerId || userId == TenantId;

    public Guid OtherParticipant(Guid userId) => userId == OwnerId ? TenantId : OwnerId;
}
