using Microsoft.AspNetCore.Mvc;

namespace RentAgency.Web.Controllers;

public class DesignController(IWebHostEnvironment environment) : Controller
{
    public IActionResult Index()
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        return View();
    }
}
