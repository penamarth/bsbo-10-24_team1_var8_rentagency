using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RentAgency.Tests.Infrastructure;
using RentAgency.Web.Features.Accounts;

namespace RentAgency.Tests.Accounts;

public class RegistrationTests
{
    [Fact]
    public async Task Registration_AssignsTenantOnly()
    {
        using var factory = new SqliteWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await AuthTestHelper.RegisterAsync(client, "tenant@test.local");

        response.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("tenant@test.local");

        Assert.NotNull(user);
        var roles = await userManager.GetRolesAsync(user!);
        Assert.Equal([UserRoles.Tenant], roles);
    }

    [Fact]
    public async Task Registration_CannotAssignModerator()
    {
        using var factory = new SqliteWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await AuthTestHelper.RegisterAsync(
            client,
            "spoof@test.local",
            extraFieldName: "Role",
            extraFieldValue: UserRoles.Moderator);

        response.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("spoof@test.local");

        Assert.NotNull(user);
        Assert.False(await userManager.IsInRoleAsync(user!, UserRoles.Moderator));
        Assert.Equal([UserRoles.Tenant], await userManager.GetRolesAsync(user!));
    }

    [Fact]
    public async Task InvalidRegistration_DoesNotCreateUser()
    {
        using var factory = new SqliteWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await AuthTestHelper.GetAntiforgeryTokenAsync(client, "/Account/Register");
        var response = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Name"] = "Без пароля",
                ["ContactDetails"] = "+7 900 000-00-00",
                ["Email"] = "invalid@test.local",
                ["Password"] = "short",
                ["ConfirmPassword"] = "different"
            }));

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.Null(await userManager.FindByEmailAsync("invalid@test.local"));
    }
}

public class AccountAuthorizationTests
{
    [Fact]
    public async Task AnonymousVisitor_IsRedirectedFromOwnerActivation()
    {
        using var factory = new SqliteWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Account/BecomeOwner");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task OwnerActivation_AddsOwnerAndPreservesTenant()
    {
        using var factory = new SqliteWebApplicationFactory();
        using var client = factory.CreateClient();

        await AuthTestHelper.RegisterAsync(client, "owner@test.local");

        var token = await AuthTestHelper.GetAntiforgeryTokenAsync(client, "/Account/BecomeOwner");
        var response = await client.PostAsync("/Account/BecomeOwner", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            }));

        response.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("owner@test.local");

        Assert.NotNull(user);
        Assert.True(await userManager.IsInRoleAsync(user!, UserRoles.Owner));
        Assert.True(await userManager.IsInRoleAsync(user!, UserRoles.Tenant));
        Assert.False(await userManager.IsInRoleAsync(user!, UserRoles.Moderator));
    }
}

public class IdentitySeederTests
{
    [Fact]
    public async Task Seeder_IsIdempotentAndCreatesNoModeratorWithoutConfiguration()
    {
        using var factory = new SqliteWebApplicationFactory(new Dictionary<string, string?>
        {
            ["Identity:Moderator:Email"] = "",
            ["Identity:Moderator:Password"] = "",
            ["Identity:Moderator:Name"] = ""
        });

        var configuration = new ConfigurationBuilder().Build();

        await IdentitySeeder.SeedAsync(factory.Services, configuration);
        await IdentitySeeder.SeedAsync(factory.Services, configuration);

        using var scope = factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var roles = roleManager.Roles.Select(r => r.Name).ToList();

        Assert.Equal(
            UserRoles.All.Length,
            roles.Count(role => UserRoles.All.Contains(role)));
        Assert.Empty(userManager.Users.ToList());
    }

    [Fact]
    public async Task Moderator_IsSeededOnlyFromExplicitConfiguration()
    {
        using var factory = new SqliteWebApplicationFactory(new Dictionary<string, string?>
        {
            ["Identity:Moderator:Email"] = "moderator@test.local",
            ["Identity:Moderator:Password"] = "Moderator1!",
            ["Identity:Moderator:Name"] = "Модератор"
        });

        // Startup seeding must see the provided configuration; trigger host creation.
        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("moderator@test.local");

        Assert.NotNull(user);
        Assert.True(await userManager.IsInRoleAsync(user!, UserRoles.Moderator));
    }
}
