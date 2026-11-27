using RentAgency.Tests.Infrastructure;
using RentAgency.Web.Infrastructure.Files;

namespace RentAgency.Tests.Infrastructure;

public class FileStorageTests : IDisposable
{
    private readonly TempDirectory _root = new();
    private readonly LocalFileStorage _storage;

    public FileStorageTests()
    {
        _storage = new LocalFileStorage(_root.Path);
    }

    public void Dispose()
    {
        _root.Dispose();
    }

    [Fact]
    public async Task Save_GeneratesKeyAndPreservesOriginalName()
    {
        var stored = await _storage.SaveAsync(
            UploadFixtures.AsStream(UploadFixtures.Jpeg()),
            UploadKind.ListingPhoto,
            "image/jpeg",
            "квартира.jpg");

        Assert.Matches("^listing/[a-f0-9]{32}\\.jpg$", stored.StorageKey);
        Assert.Equal("квартира.jpg", stored.OriginalFileName);
        Assert.Equal("image/jpeg", stored.ContentType);
        Assert.Equal(64, stored.SizeBytes);
    }

    [Fact]
    public async Task Save_IdenticalClientNamesProduceDistinctKeys()
    {
        var first = await _storage.SaveAsync(
            UploadFixtures.AsStream(UploadFixtures.Jpeg()), UploadKind.ListingPhoto, "image/jpeg", "photo.jpg");
        var second = await _storage.SaveAsync(
            UploadFixtures.AsStream(UploadFixtures.Jpeg()), UploadKind.ListingPhoto, "image/jpeg", "photo.jpg");

        Assert.NotEqual(first.StorageKey, second.StorageKey);
    }

    [Fact]
    public async Task Save_RejectsUnsupportedContentType()
    {
        await Assert.ThrowsAsync<UploadRejectedException>(() => _storage.SaveAsync(
            UploadFixtures.AsStream(UploadFixtures.Jpeg()), UploadKind.ListingPhoto, "image/gif", "photo.gif"));

        Assert.Empty(Directory.GetFiles(_root.Path, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Save_RejectsContentThatDoesNotMatchType()
    {
        await Assert.ThrowsAsync<UploadRejectedException>(() => _storage.SaveAsync(
            UploadFixtures.AsStream("not an image"u8.ToArray()),
            UploadKind.ListingPhoto,
            "image/png",
            "fake.png"));

        Assert.Empty(Directory.GetFiles(_root.Path, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Save_RejectsOversizedFileAndLeavesNothing()
    {
        var oversized = UploadFixtures.Png((int)UploadPolicy.ListingPhotoMaxBytes + 1);

        await Assert.ThrowsAsync<UploadRejectedException>(() => _storage.SaveAsync(
            UploadFixtures.AsStream(oversized), UploadKind.ListingPhoto, "image/png", "huge.png"));

        Assert.Empty(Directory.GetFiles(_root.Path, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Save_RejectsNonSeekableOversizedStreamAndCleansUp()
    {
        var oversized = UploadFixtures.Png((int)UploadPolicy.ListingPhotoMaxBytes + 1024);

        await Assert.ThrowsAsync<UploadRejectedException>(() => _storage.SaveAsync(
            new ForwardOnlyStream(oversized), UploadKind.ListingPhoto, "image/png", "huge.png"));

        Assert.Empty(Directory.GetFiles(_root.Path, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task OpenRead_ReturnsSavedBytes()
    {
        var bytes = UploadFixtures.Pdf();
        var stored = await _storage.SaveAsync(
            UploadFixtures.AsStream(bytes), UploadKind.ComplaintEvidence, "application/pdf", "evidence.pdf");

        await using var stream = await _storage.OpenReadAsync(stored.StorageKey);

        Assert.NotNull(stream);
        using var reader = new MemoryStream();
        await stream!.CopyToAsync(reader);
        Assert.Equal(bytes, reader.ToArray());
    }

    [Fact]
    public async Task OpenRead_RejectsUnknownAndUnsafeKeys()
    {
        Assert.Null(await _storage.OpenReadAsync("listing/" + new string('a', 32) + ".jpg"));
        Assert.Null(await _storage.OpenReadAsync("../secret.txt"));
        Assert.Null(await _storage.OpenReadAsync("listing/../../escape.jpg"));
    }

    [Fact]
    public async Task Delete_RemovesFile()
    {
        var stored = await _storage.SaveAsync(
            UploadFixtures.AsStream(UploadFixtures.Jpeg()), UploadKind.ListingPhoto, "image/jpeg", "photo.jpg");

        await _storage.DeleteAsync(stored.StorageKey);
        await _storage.DeleteAsync(stored.StorageKey);
        await _storage.DeleteAsync("../../../etc/passwd");

        Assert.Null(await _storage.OpenReadAsync(stored.StorageKey));
    }

    private sealed class ForwardOnlyStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;

        public override long Length => throw new NotSupportedException();
    }
}
