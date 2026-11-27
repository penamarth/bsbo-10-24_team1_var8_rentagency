using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentAgency.Tests.Infrastructure;
using RentAgency.Web.Data;
using RentAgency.Web.Features.Accounts;
using RentAgency.Web.Features.Rentals.Application;
using RentAgency.Web.Features.Rentals.Domain;

namespace RentAgency.Tests.Accounts;

public class NotificationTests
{
    [Fact]
    public async Task FailedDomainSave_CreatesNoNotification()
    {
        var interceptor = new FailNextSaveInterceptor();
        using var site = new SprintTestSite(interceptor);
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var (tenantId, _) = await site.CreateUserAsync(UserRoles.Tenant);
        var listingId = await site.PublishListingAsync(ownerId);
        var requests = site.Get<IRequestService>();
        var notifications = site.Get<INotificationService>();
        var notificationsBefore = (await notifications.GetForUserAsync(ownerId)).Count;

        interceptor.FailNext = true;

        await Assert.ThrowsAnyAsync<Exception>(() => requests.SubmitAsync(
            tenantId,
            listingId,
            new CreateRentalRequest(
                "Заявка не сохранится.",
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)))));

        using var scope = site.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RentAgencyDbContext>();

        Assert.Equal(0, await db.RentalRequests.CountAsync());
        Assert.Equal(notificationsBefore, (await notifications.GetForUserAsync(ownerId)).Count);
    }

    [Fact]
    public async Task SuccessfulModeration_NotifiesOnlyTheOwner()
    {
        using var site = new SprintTestSite();
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var (otherId, _) = await site.CreateUserAsync(UserRoles.Tenant);

        await site.PublishListingAsync(ownerId);

        var notifications = site.Get<INotificationService>();

        Assert.Single(await notifications.GetForUserAsync(ownerId));
        Assert.Empty(await notifications.GetForUserAsync(otherId));
    }
}
