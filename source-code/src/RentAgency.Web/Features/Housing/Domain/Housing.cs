namespace RentAgency.Web.Features.Housing.Domain;

public class Housing
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public string Address { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string RentalTerms { get; set; } = string.Empty;

    public List<Listing> Listings { get; set; } = [];
}
