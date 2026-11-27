using Microsoft.AspNetCore.Identity;

namespace RentAgency.Web.Features.Accounts;

public class ApplicationUser : IdentityUser<Guid>
{
    public string Name { get; set; } = string.Empty;

    public string ContactDetails { get; set; } = string.Empty;
}
