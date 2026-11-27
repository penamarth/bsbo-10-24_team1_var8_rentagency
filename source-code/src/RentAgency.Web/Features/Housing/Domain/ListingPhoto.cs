namespace RentAgency.Web.Features.Housing.Domain;

public class ListingPhoto
{
    public Guid Id { get; set; }

    public Guid ListingId { get; set; }

    public string StorageKey { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
