using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RentAgency.Web.Features.Accounts;
using RentAgency.Web.Features.Housing.Application;
using RentAgency.Web.Features.Housing.Domain;
using RentAgency.Web.Features.Rentals.Application;
using RentAgency.Web.Infrastructure;
using RentAgency.Web.Infrastructure.Files;
using RentAgency.Web.ViewModels;

namespace RentAgency.Web.Controllers;

[Authorize(Roles = UserRoles.Owner)]
public class OwnerController(
    IListingService listings,
    IRequestService requests,
    IFileStorage storage,
    UserManager<ApplicationUser> users) : Controller
{
    [HttpGet("/owner")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        return View(new OwnerDashboardViewModel
        {
            Listings = await listings.GetOwnerListingsAsync(userId.Value, cancellationToken),
            Requests = await requests.GetOwnerRequestsAsync(userId.Value, cancellationToken)
        });
    }

    [HttpGet("/owner/listings/create")]
    public IActionResult Create()
    {
        return View(new ListingFormViewModel());
    }

    [HttpPost("/owner/listings/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ListingFormViewModel form, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var photos = await SavePhotosAsync(form.Photos, cancellationToken);

        if (photos is null)
        {
            return View(form);
        }

        try
        {
            await listings.SubmitAsync(userId.Value, ToDraft(form, photos), cancellationToken);
            TempData["Success"] = "Объявление отправлено на модерацию.";
            return RedirectToAction(nameof(Index));
        }
        catch (DomainException exception)
        {
            await DeleteFilesAsync(photos, cancellationToken);
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(form);
        }
    }

    [HttpGet("/owner/listings/{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        var listing = await listings.GetOwnerListingAsync(userId.Value, id, cancellationToken);

        if (listing is null)
        {
            return NotFound();
        }

        return View(new ListingFormViewModel
        {
            ListingId = listing.ListingId,
            IsEdit = true,
            Version = listing.Version,
            Address = listing.Address,
            Description = listing.Description,
            RentalTerms = listing.RentalTerms,
            Price = listing.Price,
            Rooms = listing.Rooms,
            Area = listing.Area,
            AvailableFrom = listing.AvailableFrom,
            ExistingPhotoKeys = listing.PhotoKeys
        });
    }

    [HttpPost("/owner/listings/{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ListingFormViewModel form, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        form.ListingId = id;
        form.IsEdit = true;

        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var photos = await SavePhotosAsync(form.Photos, cancellationToken);

        if (photos is null)
        {
            return View(form);
        }

        try
        {
            var previous = await listings.GetOwnerListingAsync(userId.Value, id, cancellationToken);

            await listings.ResubmitAsync(userId.Value, id, ToDraft(form, photos), form.Version, cancellationToken);

            if (previous is not null)
            {
                foreach (var key in previous.PhotoKeys)
                {
                    await storage.DeleteAsync(key, cancellationToken);
                }
            }

            TempData["Success"] = "Объявление повторно отправлено на модерацию.";
            return RedirectToAction(nameof(Index));
        }
        catch (DomainException exception)
        {
            await DeleteFilesAsync(photos, cancellationToken);
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(form);
        }
    }

    private async Task<List<ListingPhotoInput>?> SavePhotosAsync(
        List<Microsoft.AspNetCore.Http.IFormFile>? files,
        CancellationToken cancellationToken)
    {
        var uploads = files?.Where(f => f.Length > 0).ToList() ?? [];

        if (uploads.Count is 0 or > UploadPolicy.ListingPhotoMaxCount)
        {
            ModelState.AddModelError("Photos", $"Добавьте от 1 до {UploadPolicy.ListingPhotoMaxCount} фотографий.");
            return null;
        }

        var saved = new List<ListingPhotoInput>();

        foreach (var file in uploads)
        {
            try
            {
                await using var stream = file.OpenReadStream();
                var stored = await storage.SaveAsync(
                    stream,
                    UploadKind.ListingPhoto,
                    file.ContentType,
                    file.FileName,
                    cancellationToken);

                saved.Add(new ListingPhotoInput(stored.StorageKey, stored.OriginalFileName, stored.ContentType));
            }
            catch (UploadRejectedException exception)
            {
                await DeleteFilesAsync(saved, cancellationToken);
                ModelState.AddModelError("Photos", exception.Message);
                return null;
            }
        }

        return saved;
    }

    private async Task DeleteFilesAsync(IEnumerable<ListingPhotoInput> photos, CancellationToken cancellationToken)
    {
        foreach (var photo in photos)
        {
            await storage.DeleteAsync(photo.StorageKey, cancellationToken);
        }
    }

    private static ListingDraft ToDraft(ListingFormViewModel form, IReadOnlyList<ListingPhotoInput> photos)
    {
        return new ListingDraft(
            form.Address,
            form.Description,
            form.RentalTerms,
            form.Price,
            form.Rooms,
            form.Area,
            form.AvailableFrom,
            photos);
    }

    private Guid? CurrentUserId()
    {
        return Guid.TryParse(users.GetUserId(User), out var id) ? id : null;
    }
}
