using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RentAgency.Web.Features.Housing.Application;
using RentAgency.Web.Models;
using RentAgency.Web.ViewModels;

namespace RentAgency.Web.Controllers;

public class HomeController(ISearchService search) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? location,
        decimal? maxPrice,
        int? rooms,
        CancellationToken cancellationToken)
    {
        var criteria = new SearchCriteria(
            string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            maxPrice,
            rooms);

        var results = await search.SearchAsync(criteria, cancellationToken);

        return View(new SearchPageViewModel
        {
            Location = criteria.Location,
            MaxPrice = criteria.MaxPrice,
            Rooms = criteria.Rooms,
            Results = results
        });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
