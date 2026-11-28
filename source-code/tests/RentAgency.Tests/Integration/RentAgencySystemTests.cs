using Microsoft.Extensions.DependencyInjection;
using RentAgency.Tests.Infrastructure;
using RentAgency.Web.Features;
using RentAgency.Web.Features.Accounts;
using RentAgency.Web.Features.Housing.Application;
using RentAgency.Web.Features.Housing.Domain;
using RentAgency.Web.Features.Rentals.Application;
using RentAgency.Web.Features.Rentals.Domain;

namespace RentAgency.Tests.Integration;

public class RentAgencySystemTests
{
    [Fact]
    public async Task MainFacade_CoversListingLifecycleEndToEnd()
    {
        using var site = new SprintTestSite();
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var (tenantId, _) = await site.CreateUserAsync(UserRoles.Tenant);
        var moderatorId = await site.ModeratorIdAsync();

        using var scope = site.Factory.Services.CreateScope();
        var system = scope.ServiceProvider.GetRequiredService<RentAgencySystem>();

        var listingId = await system.SubmitListingAsync(ownerId, SprintTestSite.Draft("Фасадная улица, 5"));
        var view = await site.Get<RentAgency.Web.Features.Housing.Application.IListingService>()
            .GetForModerationAsync(moderatorId, listingId);
        var outcome = await system.ModerateListingAsync(
            moderatorId, listingId, ModerationResult.Approved, null, view.Details.Version);
        Assert.True(outcome.Applied);

        var found = await system.SearchAsync(new SearchCriteria("Фасадная", null, null));
        Assert.Equal(listingId, Assert.Single(found).ListingId);

        var requestId = await system.SubmitRequestAsync(
            tenantId,
            listingId,
            new CreateRentalRequest(
                "Хочу через фасад.",
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(32))));
        await system.StartDiscussionAsync(requestId, ownerId);

        var agreementId = await system.ProposeTermsAsync(
            requestId,
            ownerId,
            new RentalTerms(
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(32)),
                45_000m,
                0m));
        await system.ConfirmTermsAsync(agreementId, ownerId, 1);
        await system.ConfirmTermsAsync(agreementId, tenantId, 1);
        await system.ConfirmCompletionAsync(agreementId, tenantId);
        await system.ConfirmCompletionAsync(agreementId, ownerId);
        var review = await system.LeaveReviewAsync(agreementId, tenantId, ownerId, "Всё честно.");

        Assert.Equal(tenantId, review.AuthorId);
    }
}
