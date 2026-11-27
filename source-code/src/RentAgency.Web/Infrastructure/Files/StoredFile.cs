namespace RentAgency.Web.Infrastructure.Files;

public enum UploadKind
{
    ListingPhoto,
    ComplaintEvidence
}

public sealed class UploadRejectedException(string message) : Exception(message);

public sealed record StoredFile(
    string StorageKey,
    string OriginalFileName,
    string ContentType,
    long SizeBytes);

public sealed class StorageOptions
{
    public string? RootPath { get; set; }
}
