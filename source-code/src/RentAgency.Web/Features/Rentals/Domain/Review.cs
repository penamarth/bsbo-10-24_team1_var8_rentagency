namespace RentAgency.Web.Features.Rentals.Domain;

public class Review
{
    public Guid Id { get; set; }

    public Guid AgreementId { get; set; }

    public Guid AuthorId { get; set; }

    public Guid RecipientId { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
