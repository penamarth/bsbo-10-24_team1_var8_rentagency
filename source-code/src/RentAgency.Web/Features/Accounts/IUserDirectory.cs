using Microsoft.AspNetCore.Identity;
using RentAgency.Web.Features.Accounts;

namespace RentAgency.Web.Features.Accounts;

public interface IUserDirectory
{
    Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> IsInRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default);
}

public sealed class UserDirectory(UserManager<ApplicationUser> userManager) : IUserDirectory
{
    public async Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await userManager.FindByIdAsync(userId.ToString()) is not null;
    }

    public async Task<bool> IsInRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is not null && await userManager.IsInRoleAsync(user, role);
    }
}
