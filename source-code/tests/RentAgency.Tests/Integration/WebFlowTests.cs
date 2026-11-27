using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentAgency.Tests.Infrastructure;
using RentAgency.Web.Data;
using RentAgency.Web.Features.Accounts;
using RentAgency.Web.Features.Housing.Application;

namespace RentAgency.Tests.Integration;

public class WebFlowTests
{
    private static HttpClient CreateNoRedirectClient(SqliteWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Home_ShowsBrandAndOnlyPublishedListings()
    {
        using var site = new SprintTestSite();
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        await site.PublishListingAsync(ownerId, SprintTestSite.Draft("Тверская улица, 10"));
        await site.SubmitListingAsync(ownerId, SprintTestSite.Draft("Скрытая улица, 11"));

        using var client = site.Factory.CreateClient();
        var html = System.Net.WebUtility.HtmlDecode(await client.GetStringAsync("/"));

        Assert.Contains("нахлебная крыша", html);
        Assert.Contains("Тверская улица, 10", html);
        Assert.DoesNotContain("Скрытая улица, 11", html);
    }

    [Fact]
    public async Task ProtectedPages_RedirectAnonymousUsersToLogin()
    {
        using var site = new SprintTestSite();
        using var client = CreateNoRedirectClient(site.Factory);

        foreach (var url in new[] { "/owner", "/moderation", "/complaints", "/rentals" })
        {
            var response = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Account/Login", response.Headers.Location?.OriginalString);
        }
    }

    [Fact]
    public async Task OwnerArea_RejectsTenantRole()
    {
        using var site = new SprintTestSite();
        var (_, tenantEmail) = await site.CreateUserAsync(UserRoles.Tenant);
        using var client = CreateNoRedirectClient(site.Factory);

        await AuthTestHelper.LoginAsync(client, tenantEmail);
        var response = await client.GetAsync("/owner");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("AccessDenied", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ListingDetails_IsPublicForPublishedAndHiddenForDraft()
    {
        using var site = new SprintTestSite();
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var publishedId = await site.PublishListingAsync(ownerId, SprintTestSite.Draft("Публичная улица, 1"));
        var draftId = await site.SubmitListingAsync(ownerId, SprintTestSite.Draft("Черновик, 2"));

        using var client = site.Factory.CreateClient();

        var published = await client.GetAsync($"/listing/{publishedId}");
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var html = System.Net.WebUtility.HtmlDecode(await published.Content.ReadAsStringAsync());
        Assert.Contains("Публичная улица, 1", html);
        Assert.Contains("Войти", html);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/listing/{draftId}")).StatusCode);
    }

    [Fact]
    public async Task Tenant_CanSubmitApplicationThroughWeb()
    {
        using var site = new SprintTestSite();
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var (_, tenantEmail) = await site.CreateUserAsync(UserRoles.Tenant);
        var listingId = await site.PublishListingAsync(ownerId);
        using var client = CreateNoRedirectClient(site.Factory);

        await AuthTestHelper.LoginAsync(client, tenantEmail);

        var token = await AuthTestHelper.GetAntiforgeryTokenAsync(client, $"/listing/{listingId}");
        var response = await client.PostAsync($"/rentals/apply/{listingId}", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Message"] = "Хочу снять эту квартиру.",
                ["ProposedStart"] = DateTime.UtcNow.AddDays(3).ToString("yyyy-MM-dd"),
                ["ProposedEnd"] = DateTime.UtcNow.AddDays(33).ToString("yyyy-MM-dd")
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/rentals/", response.Headers.Location?.OriginalString);

        using var scope = site.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RentAgencyDbContext>();
        Assert.Equal(1, await db.RentalRequests.CountAsync(r => r.ListingId == listingId));
    }

    [Fact]
    public async Task Moderator_SeesQueueAndCanOpenListing()
    {
        using var site = new SprintTestSite();
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var listingId = await site.SubmitListingAsync(ownerId, SprintTestSite.Draft("На проверке, 3"));
        var moderatorId = await site.ModeratorIdAsync();
        var moderatorEmail = site.ModeratorEmail;
        using var client = CreateNoRedirectClient(site.Factory);

        await AuthTestHelper.LoginAsync(client, moderatorEmail);

        var queue = await client.GetAsync("/moderation");
        Assert.Equal(HttpStatusCode.OK, queue.StatusCode);
        Assert.Contains("На проверке, 3", System.Net.WebUtility.HtmlDecode(await queue.Content.ReadAsStringAsync()));

        var details = await client.GetAsync($"/moderation/{listingId}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);

        var tenants = await site.CreateUserAsync(UserRoles.Tenant);
        _ = moderatorId;
        _ = tenants;
    }
}
