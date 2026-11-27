using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentAgency.Web.Data;
using RentAgency.Web.Features.Accounts;
using RentAgency.Web.Features.Complaints.Application;
using RentAgency.Web.Infrastructure;
using RentAgency.Web.Infrastructure.Files;
using RentAgency.Web.ViewModels;

namespace RentAgency.Web.Controllers;

[Authorize]
public class ComplaintsController(
    IComplaintService complaints,
    IFileStorage storage,
    RentAgencyDbContext db,
    UserManager<ApplicationUser> users) : Controller
{
    [HttpGet("/complaints")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        return View(await complaints.GetMineAsync(userId.Value, cancellationToken));
    }

    [HttpGet("/complaints/create")]
    public async Task<IActionResult> Create(
        Guid? listingId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        if ((listingId is null) == (userId is null))
        {
            return BadRequest();
        }

        return View(new ComplaintCreateViewModel
        {
            ListingId = listingId,
            TargetUserId = userId,
            TargetLabel = await TargetLabelAsync(listingId, userId, cancellationToken)
        });
    }

    [HttpPost("/complaints/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ComplaintCreateViewModel form, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        form.TargetLabel = await TargetLabelAsync(form.ListingId, form.TargetUserId, cancellationToken);

        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var uploads = form.Evidence?.Where(f => f.Length > 0).ToList() ?? [];

        if (uploads.Count > UploadPolicy.ComplaintEvidenceMaxCount)
        {
            ModelState.AddModelError("Evidence", $"Можно приложить не более {UploadPolicy.ComplaintEvidenceMaxCount} файлов.");
            return View(form);
        }

        var saved = new List<UploadInput>();

        try
        {
            foreach (var file in uploads)
            {
                await using var stream = file.OpenReadStream();
                var stored = await storage.SaveAsync(
                    stream,
                    UploadKind.ComplaintEvidence,
                    file.ContentType,
                    file.FileName,
                    cancellationToken);

                saved.Add(new UploadInput(stored.StorageKey, stored.OriginalFileName, stored.ContentType));
            }

            var complaintId = await complaints.SubmitAsync(
                userId.Value,
                new ComplaintTarget(form.ListingId, form.TargetUserId),
                form.Description,
                saved,
                cancellationToken);

            TempData["Success"] = "Жалоба отправлена.";
            return RedirectToAction(nameof(Details), new { id = complaintId });
        }
        catch (UploadRejectedException exception)
        {
            await DeleteFilesAsync(saved, cancellationToken);
            ModelState.AddModelError("Evidence", exception.Message);
            return View(form);
        }
        catch (DomainException exception)
        {
            await DeleteFilesAsync(saved, cancellationToken);
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(form);
        }
    }

    [HttpGet("/complaints/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        var complaint = await complaints.GetAsync(id, userId.Value, cancellationToken);

        return View(new ComplaintDetailsPageViewModel
        {
            Complaint = complaint,
            TargetLabel = await TargetLabelAsync(complaint.TargetListingId, complaint.TargetUserId, cancellationToken)
        });
    }

    private async Task DeleteFilesAsync(IEnumerable<UploadInput> files, CancellationToken cancellationToken)
    {
        foreach (var file in files)
        {
            await storage.DeleteAsync(file.StorageKey, cancellationToken);
        }
    }

    private async Task<string> TargetLabelAsync(Guid? listingId, Guid? targetUserId, CancellationToken cancellationToken)
    {
        if (listingId is not null)
        {
            return await db.Listings
                .AsNoTracking()
                .Where(l => l.Id == listingId)
                .Select(l => l.Housing.Address)
                .FirstOrDefaultAsync(cancellationToken) ?? "Объявление";
        }

        if (targetUserId is not null)
        {
            var user = await users.FindByIdAsync(targetUserId.Value.ToString());
            return user?.Name ?? user?.Email ?? "Пользователь";
        }

        return "—";
    }

    private Guid? CurrentUserId()
    {
        return Guid.TryParse(users.GetUserId(User), out var id) ? id : null;
    }
}
