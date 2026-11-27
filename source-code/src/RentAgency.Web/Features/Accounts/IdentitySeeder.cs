using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace RentAgency.Web.Features.Accounts;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in UserRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(role));

                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Failed to create role '{role}': {string.Join("; ", roleResult.Errors.Select(e => e.Description))}");
                }
            }
        }

        var email = configuration["Identity:Moderator:Email"];
        var password = configuration["Identity:Moderator:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var moderator = await userManager.FindByEmailAsync(email);

        if (moderator is null)
        {
            moderator = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                Name = configuration["Identity:Moderator:Name"] ?? "Модератор",
                ContactDetails = string.Empty
            };

            var createResult = await userManager.CreateAsync(moderator, password);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create moderator '{email}': {string.Join("; ", createResult.Errors.Select(e => e.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(moderator, UserRoles.Moderator))
        {
            var roleResult = await userManager.AddToRoleAsync(moderator, UserRoles.Moderator);

            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to assign moderator role: {string.Join("; ", roleResult.Errors.Select(e => e.Description))}");
            }
        }
    }
}
