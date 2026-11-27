using System.Text.RegularExpressions;

namespace RentAgency.Web.Infrastructure.Files;

public sealed partial class LocalFileStorage : IFileStorage
{
    private const int HeaderLength = 16;

    private readonly string _rootPath;

    public LocalFileStorage(string rootPath)
    {
        _rootPath = Path.GetFullPath(rootPath);
    }

    public async Task<StoredFile> SaveAsync(
        Stream content,
        UploadKind kind,
        string contentType,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        var prefix = KindPrefix(kind);
        var extension = UploadPolicy.ExtensionFor(contentType);
        var buffer = new byte[HeaderLength];
        var headerLength = 0;

        while (headerLength < HeaderLength)
        {
            var read = await content.ReadAsync(buffer.AsMemory(headerLength), cancellationToken);

            if (read == 0)
            {
                break;
            }

            headerLength += read;
        }

        var declaredLength = content.CanSeek ? content.Length : -1;
        UploadPolicy.EnsureAccepted(kind, contentType, declaredLength, buffer.AsSpan(0, headerLength));

        var storageKey = $"{prefix}/{Guid.NewGuid():N}{extension}";
        var path = ResolvePath(storageKey);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        long written = 0;

        try
        {
            await using var target = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            await target.WriteAsync(buffer.AsMemory(0, headerLength), cancellationToken);
            written = headerLength;

            var chunk = new byte[81920];

            while (true)
            {
                var read = await content.ReadAsync(chunk, cancellationToken);

                if (read == 0)
                {
                    break;
                }

                written += read;

                if (written > UploadPolicy.MaxBytes(kind))
                {
                    throw new UploadRejectedException("File is too large.");
                }

                await target.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            }
        }
        catch
        {
            TryDelete(path);
            throw;
        }

        return new StoredFile(storageKey, originalFileName, contentType, written);
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (!IsSafeKey(storageKey))
        {
            return Task.FromResult<Stream?>(null);
        }

        var path = ResolvePath(storageKey);

        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        if (IsSafeKey(storageKey))
        {
            TryDelete(ResolvePath(storageKey));
        }

        return Task.CompletedTask;
    }

    private static bool IsSafeKey(string storageKey)
    {
        return StorageKeyPattern().IsMatch(storageKey);
    }

    private string ResolvePath(string storageKey)
    {
        var path = Path.GetFullPath(Path.Combine(_rootPath, storageKey));

        if (!path.StartsWith(_rootPath + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new UploadRejectedException("Invalid storage key.");
        }

        return path;
    }

    private static string KindPrefix(UploadKind kind)
    {
        return kind switch
        {
            UploadKind.ListingPhoto => "listing",
            UploadKind.ComplaintEvidence => "evidence",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [GeneratedRegex(@"^(listing|evidence)/[a-f0-9]{32}\.(jpg|png|webp|pdf)$")]
    private static partial Regex StorageKeyPattern();
}
