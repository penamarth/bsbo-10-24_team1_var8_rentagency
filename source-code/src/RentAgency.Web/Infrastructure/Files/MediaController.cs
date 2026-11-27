using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentAgency.Web.Data;
using RentAgency.Web.Features.Accounts;
using RentAgency.Web.Features.Housing.Domain;

namespace RentAgency.Web.Infrastructure.Files;

[Route("media")]
public partial class MediaController(
    RentAgencyDbContext db,
    IUserDirectory users,
    IFileStorage storage) : Controller
{
    [HttpGet("listings/{fileName}")]
    [AllowAnonymous]
    public async Task<IActionResult> ListingPhoto(string fileName, CancellationToken cancellationToken)
    {
        if (!ListingFileName().IsMatch(fileName))
        {
            return NotFound();
        }

        var storageKey = $"listing/{fileName}";

        var info = await (
                from photo in db.ListingPhotos.AsNoTracking()
                join listing in db.Listings.AsNoTracking() on photo.ListingId equals listing.Id
                join housing in db.Housings.AsNoTracking() on listing.HousingId equals housing.Id
                where photo.StorageKey == storageKey
                select new { listing.Status, housing.OwnerId })
            .FirstOrDefaultAsync(cancellationToken);

        if (info is null)
        {
            return NotFound();
        }

        var publiclyVisible = info.Status is ListingStatus.Published or ListingStatus.Rented;

        if (!publiclyVisible)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userIdValue is null || !Guid.TryParse(userIdValue, out var userId))
            {
                return NotFound();
            }

            var allowed = info.OwnerId == userId
                || await users.IsInRoleAsync(userId, UserRoles.Moderator, cancellationToken);

            if (!allowed)
            {
                return NotFound();
            }
        }

        var stream = await storage.OpenReadAsync(storageKey, cancellationToken);

        if (stream is null)
        {
            return NotFound();
        }

        var contentType = UploadPolicy.ContentTypeForExtension(Path.GetExtension(fileName));
        return File(stream, contentType);
    }

    [GeneratedRegex(@"^[a-f0-9]{32}\.(jpg|png|webp)$")]
    private static partial Regex ListingFileName();
}
