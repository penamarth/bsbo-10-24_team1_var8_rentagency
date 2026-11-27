namespace RentAgency.Web.Infrastructure.Files;

public static class PhotoUrls
{
    public static string Listing(string storageKey)
    {
        return $"/media/listings/{Path.GetFileName(storageKey)}";
    }
}
