namespace RentAgency.Web.Infrastructure.Files;

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(
        Stream content,
        UploadKind kind,
        string contentType,
        string originalFileName,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
