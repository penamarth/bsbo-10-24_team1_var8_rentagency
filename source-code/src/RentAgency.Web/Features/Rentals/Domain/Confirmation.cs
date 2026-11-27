namespace RentAgency.Web.Features.Rentals.Domain;

public class Confirmation
{
    public Guid Id { get; set; }

    public Guid AgreementId { get; set; }

    public Guid ParticipantId { get; set; }

    public ConfirmationType Type { get; set; }

    public int? TermsVersion { get; set; }

    public DateTime ConfirmedAt { get; set; }
}
