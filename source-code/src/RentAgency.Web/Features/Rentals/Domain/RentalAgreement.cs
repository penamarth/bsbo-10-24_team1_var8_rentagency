namespace RentAgency.Web.Features.Rentals.Domain;

public class RentalAgreement
{
    public Guid Id { get; set; }

    public Guid RequestId { get; set; }

    public RentalRequest Request { get; set; } = null!;

    public AgreementStatus Status { get; set; }

    public RentalTerms Terms { get; set; } = null!;

    public int TermsVersion { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public List<Confirmation> Confirmations { get; set; } = [];

    public List<Review> Reviews { get; set; } = [];
}
