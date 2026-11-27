namespace RentAgency.Web.Features.Housing.Domain;

public class Listing
{
    public Guid Id { get; set; }

    public Guid HousingId { get; set; }

    public Housing Housing { get; set; } = null!;

    public decimal Price { get; set; }

    public int Rooms { get; set; }

    public double Area { get; set; }

    public DateOnly AvailableFrom { get; set; }

    public ListingStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PublishedAt { get; set; }

    public int Version { get; set; }

    public List<ListingPhoto> Photos { get; set; } = [];

    public List<ModerationDecision> Decisions { get; set; } = [];

    public List<ListingStatusChange> StatusHistory { get; set; } = [];

    public ListingStatusChange ChangeStatus(ListingStatus status, DateTime now)
    {
        Status = status;
        Version++;
        var change = new ListingStatusChange
        {
            Id = Guid.NewGuid(),
            ListingId = Id,
            Status = status,
            ChangedAt = now
        };

        StatusHistory.Add(change);

        if (status == ListingStatus.Published && PublishedAt is null)
        {
            PublishedAt = now;
        }

        return change;
    }
}
