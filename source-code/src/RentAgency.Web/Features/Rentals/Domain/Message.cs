namespace RentAgency.Web.Features.Rentals.Domain;

public class Message
{
    public Guid Id { get; set; }

    public Guid RequestId { get; set; }

    public Guid AuthorId { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTime SentAt { get; set; }
}
