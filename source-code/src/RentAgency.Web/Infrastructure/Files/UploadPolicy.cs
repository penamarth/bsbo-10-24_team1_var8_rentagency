namespace RentAgency.Web.Infrastructure.Files;

public static class UploadPolicy
{
    public const long ListingPhotoMaxBytes = 5 * 1024 * 1024;

    public const int ListingPhotoMaxCount = 8;

    public const long ComplaintEvidenceMaxBytes = 10 * 1024 * 1024;

    public const int ComplaintEvidenceMaxCount = 3;

    private static readonly string[] ImageTypes = ["image/jpeg", "image/png", "image/webp"];

    private static readonly string[] EvidenceTypes = ["image/jpeg", "image/png", "application/pdf"];

    public static IReadOnlyList<string> AllowedContentTypes(UploadKind kind)
    {
        return kind switch
        {
            UploadKind.ListingPhoto => ImageTypes,
            UploadKind.ComplaintEvidence => EvidenceTypes,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    public static long MaxBytes(UploadKind kind)
    {
        return kind switch
        {
            UploadKind.ListingPhoto => ListingPhotoMaxBytes,
            UploadKind.ComplaintEvidence => ComplaintEvidenceMaxBytes,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    public static int MaxCount(UploadKind kind)
    {
        return kind switch
        {
            UploadKind.ListingPhoto => ListingPhotoMaxCount,
            UploadKind.ComplaintEvidence => ComplaintEvidenceMaxCount,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    public static string ExtensionFor(string contentType)
    {
        return contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "application/pdf" => ".pdf",
            _ => throw new UploadRejectedException("Unsupported file type.")
        };
    }

    public static string ContentTypeForExtension(string extension)
    {
        return extension switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }

    public static void EnsureAccepted(UploadKind kind, string contentType, long length, ReadOnlySpan<byte> header)
    {
        if (!AllowedContentTypes(kind).Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new UploadRejectedException("Unsupported file type.");
        }

        if (length == 0)
        {
            throw new UploadRejectedException("File is empty.");
        }

        if (length > MaxBytes(kind))
        {
            throw new UploadRejectedException("File is too large.");
        }

        if (!HasValidSignature(contentType, header))
        {
            throw new UploadRejectedException("File content does not match its type.");
        }
    }

    private static bool HasValidSignature(string contentType, ReadOnlySpan<byte> header)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => header.Length >= 3
                && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            "image/png" => header.Length >= 8
                && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "image/webp" => header.Length >= 12
                && header[..4].SequenceEqual("RIFF"u8)
                && header[8..12].SequenceEqual("WEBP"u8),
            "application/pdf" => header.Length >= 5
                && header[..5].SequenceEqual("%PDF-"u8),
            _ => false
        };
    }
}
