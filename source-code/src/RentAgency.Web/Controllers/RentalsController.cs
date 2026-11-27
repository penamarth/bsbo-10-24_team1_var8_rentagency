using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentAgency.Web.Data;
using RentAgency.Web.Features.Accounts;
using RentAgency.Web.Features.Rentals.Application;
using RentAgency.Web.Features.Rentals.Domain;
using RentAgency.Web.Infrastructure;
using RentAgency.Web.ViewModels;

namespace RentAgency.Web.Controllers;

[Authorize]
public class RentalsController(
    IRequestService requests,
    IRentalService rentals,
    RentAgencyDbContext db,
    UserManager<ApplicationUser> users) : Controller
{
    [HttpGet("/rentals")]
    public async Task<IActionResult> Index(string? tab, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        ViewData["Tab"] = tab == "owner" ? "owner" : "tenant";

        return View(new RequestListViewModel
        {
            AsTenant = await requests.GetTenantRequestsAsync(userId.Value, cancellationToken),
            AsOwner = await requests.GetOwnerRequestsAsync(userId.Value, cancellationToken)
        });
    }

    [HttpGet("/rentals/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        var request = await requests.GetRequestAsync(id, userId.Value, cancellationToken);
        var listing = await db.Listings
            .AsNoTracking()
            .Where(l => l.Id == request.ListingId)
            .Select(l => new { l.Housing.Address, l.Housing.OwnerId })
            .FirstOrDefaultAsync(cancellationToken);

        var owner = await users.FindByIdAsync(request.OwnerId.ToString());
        var tenant = await users.FindByIdAsync(request.TenantId.ToString());
        IReadOnlyList<ReviewView> reviews = request.Agreement is null
            ? new List<ReviewView>()
            : await rentals.GetReviewsAsync(request.Agreement.AgreementId, userId.Value, cancellationToken);

        return View(new RentalWorkspaceViewModel
        {
            Request = request,
            Address = listing?.Address ?? "—",
            OwnerName = owner?.Name ?? owner?.Email ?? "Арендодатель",
            TenantName = tenant?.Name ?? tenant?.Email ?? "Арендатор",
            CurrentUserId = userId.Value,
            IsOwner = request.OwnerId == userId.Value,
            OtherPartyName = request.OwnerId == userId.Value ? tenant?.Name : owner?.Name,
            Agreement = request.Agreement,
            Reviews = reviews,
            HasReview = reviews.Any(r => r.AuthorId == userId.Value),
            Terms = new RentalTermsInput
            {
                Start = request.ProposedStart,
                End = request.ProposedEnd,
                MonthlyPrice = 0,
                Deposit = 0
            }
        });
    }

    [HttpPost("/rentals/apply/{listingId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(
        Guid listingId,
        ApplyForRentalViewModel model,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        try
        {
            var requestId = await requests.SubmitAsync(
                userId.Value,
                listingId,
                new CreateRentalRequest(model.Message, model.ProposedStart, model.ProposedEnd),
                cancellationToken);

            TempData["Success"] = "Заявка отправлена.";
            return RedirectToAction(nameof(Details), new { id = requestId });
        }
        catch (DomainException exception)
        {
            TempData["Error"] = exception.Message;
            return RedirectToAction("Details", "Listing", new { id = listingId });
        }
    }

    [HttpPost("/rentals/{id:guid}/discussion")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartDiscussion(Guid id)
    {
        return await RunAsync(id, userId => requests.StartDiscussionAsync(id, userId), "Обсуждение начато.");
    }

    [HttpPost("/rentals/{id:guid}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid id, string reason)
    {
        return await RunAsync(id, userId => requests.RejectAsync(id, userId, reason), "Заявка отклонена.");
    }

    [HttpPost("/rentals/{id:guid}/messages")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(Guid id, string text)
    {
        return await RunAsync(id, userId => requests.SendMessageAsync(id, userId, text), "Сообщение отправлено.");
    }

    [HttpPost("/rentals/{id:guid}/propose")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProposeTerms(Guid id, RentalTermsInput terms)
    {
        return await RunAsync(
            id,
            userId => requests.ProposeAgreementAsync(
                id,
                userId,
                new RentalTerms(terms.Start, terms.End, terms.MonthlyPrice, terms.Deposit)),
            "Условия предложены.");
    }

    [HttpPost("/rentals/{id:guid}/confirm-terms")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmTerms(Guid id, Guid agreementId, int termsVersion)
    {
        return await RunAsync(
            id,
            userId => requests.ConfirmAgreementAsync(agreementId, userId, termsVersion),
            "Условия подтверждены.");
    }

    [HttpPost("/rentals/{id:guid}/confirm-completion")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmCompletion(Guid id, Guid agreementId)
    {
        return await RunAsync(
            id,
            userId => rentals.ConfirmCompletionAsync(agreementId, userId),
            "Завершение подтверждено.");
    }

    [HttpPost("/rentals/{id:guid}/review")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(Guid id, Guid agreementId, string text)
    {
        var userId = CurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        try
        {
            var request = await requests.GetRequestAsync(id, userId.Value);
            var recipientId = request.OwnerId == userId.Value ? request.TenantId : request.OwnerId;
            await rentals.LeaveReviewAsync(agreementId, userId.Value, recipientId, text);
            TempData["Success"] = "Отзыв сохранён.";
        }
        catch (DomainException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<IActionResult> RunAsync(Guid requestId, Func<Guid, Task> action, string success)
    {
        var userId = CurrentUserId();

        if (userId is null)
        {
            return Challenge();
        }

        try
        {
            await action(userId.Value);
            TempData["Success"] = success;
        }
        catch (DomainException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Details), new { id = requestId });
    }

    private Guid? CurrentUserId()
    {
        return Guid.TryParse(users.GetUserId(User), out var id) ? id : null;
    }
}
