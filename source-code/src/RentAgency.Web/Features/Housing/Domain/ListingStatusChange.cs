namespace RentAgency.Web.Features.Housing.Domain;

public class ListingStatusChange
{
    public Guid Id { get; set; }

    public Guid ListingId { get; set; }

    public ListingStatus Status { get; set; }

    public DateTime ChangedAt { get; set; }
}
