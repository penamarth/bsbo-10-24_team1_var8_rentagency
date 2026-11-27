namespace RentAgency.Web.Features.Housing.Domain;

public sealed record ListingPhotoInput(string StorageKey, string OriginalFileName, string ContentType);

public sealed record ListingDraft(
    string Address,
    string Description,
    string RentalTerms,
    decimal Price,
    int Rooms,
    double Area,
    DateOnly AvailableFrom,
    IReadOnlyList<ListingPhotoInput> Photos);
