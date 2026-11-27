using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentAgency.Web.Data;
using RentAgency.Web.Features.Accounts;
using RentAgency.Web.Features.Complaints.Application;
using RentAgency.Web.Features.Housing.Application;
using RentAgency.Web.Features.Housing.Domain;
using RentAgency.Web.ViewModels;

namespace RentAgency.Web.Controllers;

[Authorize(Roles = UserRoles.Moderator)]
public class ModerationController(
    IListingService listings,
    IComplaintService complaints,
    RentAgencyDbContext db,
    UserManager<ApplicationUser> users) : Controller
{
    [HttpGet("/moderation")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var moderatorId = CurrentUserId();

        if (moderatorId is null)
        {
            return Challenge();
        }

        return View(await listings.GetModerationQueueAsync(moderatorId.Value, cancellationToken));
    }

    [HttpGet("/moderation/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var moderatorId = CurrentUserId();

        if (moderatorId is null)
        {
            return Challenge();
        }

        var view = await listings.GetForModerationAsync(moderatorId.Value, id, cancellationToken);

        return View(new ModerationDetailsPageViewModel { Moderation = view });
    }

    [HttpPost("/moderation/{id:guid}/decide")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(
        Guid id,
        ModerationDecisionInput input,
        CancellationToken cancellationToken)
    {
        var moderatorId = CurrentUserId();

        if (moderatorId is null)
        {
            return Challenge();
        }

        if (!Enum.TryParse<ModerationResult>(input.Result, out var result))
        {
            TempData["Error"] = "Некорректное решение.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            var outcome = await listings.ModerateAsync(
                moderatorId.Value,
                id,
                result,
                input.Remarks,
                input.ExpectedVersion,
                cancellationToken);

            TempData[outcome.Applied ? "Success" : "Error"] = outcome.Message;
        }
        catch (Infrastructure.DomainException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet("/moderation/complaints")]
    public async Task<IActionResult> Complaints(CancellationToken cancellationToken)
    {
        var moderatorId = CurrentUserId();

        if (moderatorId is null)
        {
            return Challenge();
        }

        return View(await complaints.GetForModeratorAsync(moderatorId.Value, cancellationToken));
    }

    [HttpGet("/moderation/complaints/{id:guid}")]
    public async Task<IActionResult> Complaint(Guid id, CancellationToken cancellationToken)
    {
        var moderatorId = CurrentUserId();

        if (moderatorId is null)
        {
            return Challenge();
        }

        var complaint = await complaints.GetAsync(id, moderatorId.Value, cancellationToken);

        return View(new ModerationComplaintPageViewModel
        {
            Complaint = complaint,
            TargetLabel = await TargetLabelAsync(complaint.TargetListingId, complaint.TargetUserId, cancellationToken)
        });
    }

    [HttpPost("/moderation/complaints/{id:guid}/request-details")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestDetails(Guid id, string details, CancellationToken cancellationToken)
    {
        var moderatorId = CurrentUserId();

        if (moderatorId is null)
        {
            return Challenge();
        }

        try
        {
            await complaints.RequestDetailsAsync(moderatorId.Value, id, details, cancellationToken);
            TempData["Success"] = "Запрос уточнений сохранён.";
        }
        catch (Infrastructure.DomainException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Complaint), new { id });
    }

    [HttpPost("/moderation/complaints/{id:guid}/resolve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(
        Guid id,
        string decision,
        string? rationale,
        CancellationToken cancellationToken)
    {
        var moderatorId = CurrentUserId();

        if (moderatorId is null)
        {
            return Challenge();
        }

        try
        {
            await complaints.ResolveAsync(moderatorId.Value, id, decision, rationale, cancellationToken);
            TempData["Success"] = "Жалоба рассмотрена.";
        }
        catch (Infrastructure.DomainException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Complaint), new { id });
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
