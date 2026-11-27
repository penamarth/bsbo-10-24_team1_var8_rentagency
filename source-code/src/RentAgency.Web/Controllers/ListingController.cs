using Microsoft.AspNetCore.Mvc;
using RentAgency.Web.Features.Housing.Application;
using RentAgency.Web.ViewModels;

namespace RentAgency.Web.Controllers;

public class ListingController(ISearchService search) : Controller
{
    [HttpGet("/listing/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var listing = await search.GetPublishedListingAsync(id, cancellationToken);

        if (listing is null)
        {
            return NotFound();
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return View(new ListingDetailsPageViewModel
        {
            Listing = listing,
            IsAuthenticated = User.Identity?.IsAuthenticated is true,
            IsOwner = listing.OwnerId.ToString() == userId,
            Apply = new ApplyForRentalViewModel { ListingId = id }
        });
    }
}
