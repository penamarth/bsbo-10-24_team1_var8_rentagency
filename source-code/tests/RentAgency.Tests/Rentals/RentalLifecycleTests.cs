using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RentAgency.Tests.Infrastructure;
using RentAgency.Web.Data;
using RentAgency.Web.Features.Accounts;
using RentAgency.Web.Features.Housing.Application;
using RentAgency.Web.Features.Housing.Domain;
using RentAgency.Web.Features.Rentals.Application;
using RentAgency.Web.Features.Rentals.Domain;
using RentAgency.Web.Infrastructure;

namespace RentAgency.Tests.Rentals;

public class RequestSubmissionTests
{
    private static CreateRentalRequest Request() => new(
        "Здравствуйте, хочу снять.",
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)));

    [Fact]
    public async Task Submit_UnpublishedListingCreatesNothing()
    {
        using var site = new SprintTestSite();
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var (tenantId, _) = await site.CreateUserAsync(UserRoles.Tenant);
        var draftId = await site.SubmitListingAsync(ownerId);

        await Assert.ThrowsAsync<ConflictException>(() => site.Get<IRequestService>().SubmitAsync(
            tenantId, draftId, Request()));

        Assert.Equal(0, await site.Get<RentAgencyDbContext>().RentalRequests.CountAsync());
    }

    [Fact]
    public async Task Submit_RequiresMessageAndValidDates()
    {
        using var site = new SprintTestSite();
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var (tenantId, _) = await site.CreateUserAsync(UserRoles.Tenant);
        var listingId = await site.PublishListingAsync(ownerId);
        var requests = site.Get<IRequestService>();

        await Assert.ThrowsAsync<ValidationException>(() => requests.SubmitAsync(
            tenantId, listingId, Request() with { InitialMessage = " " }));

        await Assert.ThrowsAsync<ValidationException>(() => requests.SubmitAsync(
            tenantId, listingId, Request() with { ProposedEnd = Request().ProposedStart.AddDays(-1) }));
    }

    [Fact]
    public async Task Submit_DuplicateActiveRejectedButRetryAfterRejectionAllowed()
    {
        using var site = new SprintTestSite();
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var (tenantId, _) = await site.CreateUserAsync(UserRoles.Tenant);
        var listingId = await site.PublishListingAsync(ownerId);
        var requests = site.Get<IRequestService>();

        var requestId = await requests.SubmitAsync(tenantId, listingId, Request());

        await Assert.ThrowsAsync<ConflictException>(
            () => requests.SubmitAsync(tenantId, listingId, Request()));

        await requests.RejectAsync(requestId, ownerId, "Занято");

        var retryId = await requests.SubmitAsync(tenantId, listingId, Request());
        Assert.NotEqual(requestId, retryId);
    }

    [Fact]
    public async Task ViewingListing_DoesNotCreateRequest()
    {
        using var site = new SprintTestSite();
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var (_, _) = await site.CreateUserAsync(UserRoles.Tenant);
        var listingId = await site.PublishListingAsync(ownerId);

        await site.Get<ISearchService>().GetPublishedListingAsync(listingId);

        Assert.Equal(0, await site.Get<RentAgencyDbContext>().RentalRequests.CountAsync());
    }
}

public class DiscussionAndAgreementTests
{
    private static CreateRentalRequest Request() => new(
        "Здравствуйте, хочу снять.",
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)));

    private static RentalTerms Terms(decimal price = 50_000m) => new(
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(70)),
        price,
        10_000m);

    private static async Task<(SprintTestSite Site, Guid OwnerId, Guid TenantId, Guid ListingId, Guid RequestId)>
        ArrangeAsync(IInterceptor? interceptor = null)
    {
        var site = new SprintTestSite(interceptor);
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var (tenantId, _) = await site.CreateUserAsync(UserRoles.Tenant);
        var listingId = await site.PublishListingAsync(ownerId);
        var requestId = await site.Get<IRequestService>().SubmitAsync(tenantId, listingId, Request());
        return (site, ownerId, tenantId, listingId, requestId);
    }

    [Fact]
    public async Task Outsider_CannotReadOrMessageRequest()
    {
        var (site, ownerId, tenantId, _, requestId) = await ArrangeAsync();
        using var _site = site;
        var (outsiderId, _) = await site.CreateUserAsync(UserRoles.Tenant);
        var requests = site.Get<IRequestService>();
        _ = ownerId;
        _ = tenantId;

        await Assert.ThrowsAsync<ForbiddenException>(() => requests.GetRequestAsync(requestId, outsiderId));
        await Assert.ThrowsAsync<ForbiddenException>(
            () => requests.SendMessageAsync(requestId, outsiderId, "Привет"));
    }

    [Fact]
    public async Task Owner_CanStartDiscussionAndRejectOnlyOwnRequest()
    {
        var (site, ownerId, _, _, requestId) = await ArrangeAsync();
        using var _site = site;
        var (otherOwnerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var requests = site.Get<IRequestService>();

        await Assert.ThrowsAsync<ForbiddenException>(() => requests.StartDiscussionAsync(requestId, otherOwnerId));

        await requests.StartDiscussionAsync(requestId, ownerId);
        var view = await requests.GetRequestAsync(requestId, ownerId);
        Assert.Equal(RequestStatus.InDiscussion, view.Status);

        await requests.RejectAsync(requestId, ownerId, "Не подходит");
        view = await requests.GetRequestAsync(requestId, ownerId);
        Assert.Equal(RequestStatus.Rejected, view.Status);
        Assert.Equal("Не подходит", view.RejectionReason);
    }

    [Fact]
    public async Task Messages_AreStoredWithAuthorAndTime()
    {
        var (site, ownerId, tenantId, _, requestId) = await ArrangeAsync();
        using var _site = site;
        var requests = site.Get<IRequestService>();

        await requests.SendMessageAsync(requestId, tenantId, "Когда можно посмотреть?");
        await requests.SendMessageAsync(requestId, ownerId, "В субботу.");

        var view = await requests.GetRequestAsync(requestId, tenantId);
        Assert.Equal(2, view.Messages.Count);
        Assert.Equal([tenantId, ownerId], view.Messages.Select(m => m.AuthorId));
    }

    [Fact]
    public async Task TermsVersioning_RejectsOldConfirmationAndIgnoresIt()
    {
        var (site, ownerId, tenantId, _, requestId) = await ArrangeAsync();
        using var _site = site;
        var requests = site.Get<IRequestService>();

        var agreementId = await requests.ProposeAgreementAsync(requestId, tenantId, Terms());
        await requests.ConfirmAgreementAsync(agreementId, tenantId, 1);

        await requests.ProposeAgreementAsync(requestId, ownerId, Terms(55_000m));

        await Assert.ThrowsAsync<ConflictException>(
            () => requests.ConfirmAgreementAsync(agreementId, ownerId, 1));

        await requests.ConfirmAgreementAsync(agreementId, ownerId, 2);
        var agreement = await requests.GetAgreementAsync(agreementId, ownerId);

        Assert.Equal(2, agreement.TermsVersion);
        Assert.Equal(55_000m, agreement.Terms.MonthlyPrice);
        Assert.Equal([ownerId], agreement.TermsConfirmedBy);
        Assert.Equal(AgreementStatus.Draft, agreement.Status);

        var db = site.Get<RentAgencyDbContext>();
        var listingId = await db.RentalRequests
            .Where(r => r.Id == requestId)
            .Select(r => r.ListingId)
            .FirstAsync();
        var listing = await db.Listings.AsNoTracking().SingleAsync(l => l.Id == listingId);
        Assert.Equal(ListingStatus.Published, listing.Status);
    }

    [Fact]
    public async Task SecondConfirmation_AtomicallyRentsListingAndRejectsCompetingRequests()
    {
        var (site, ownerId, tenantId, listingId, requestId) = await ArrangeAsync();
        using var _site = site;
        var requests = site.Get<IRequestService>();
        var (otherTenantId, _) = await site.CreateUserAsync(UserRoles.Tenant);
        var competingId = await requests.SubmitAsync(otherTenantId, listingId, Request());

        var agreementId = await requests.ProposeAgreementAsync(requestId, tenantId, Terms());
        await requests.ConfirmAgreementAsync(agreementId, ownerId, 1);
        await requests.ConfirmAgreementAsync(agreementId, tenantId, 1);

        var db = site.Get<RentAgencyDbContext>();
        var listing = await db.Listings.AsNoTracking().SingleAsync(l => l.Id == listingId);
        var request = await db.RentalRequests.AsNoTracking().SingleAsync(r => r.Id == requestId);
        var competing = await db.RentalRequests.AsNoTracking().SingleAsync(r => r.Id == competingId);

        Assert.Equal(ListingStatus.Rented, listing.Status);
        Assert.Equal(RequestStatus.Rented, request.Status);
        Assert.Equal(RequestStatus.Rejected, competing.Status);

        var search = await site.Get<ISearchService>().SearchAsync(new SearchCriteria(null, null, null));
        Assert.Empty(search);
    }

    [Fact]
    public async Task FailedSecondConfirmation_PreservesFirstAndKeepsListingPublished()
    {
        var interceptor = new FailNextSaveInterceptor(0);
        var (site, ownerId, tenantId, listingId, requestId) = await ArrangeAsync(interceptor);
        using var _site = site;
        var requests = site.Get<IRequestService>();

        var agreementId = await requests.ProposeAgreementAsync(requestId, tenantId, Terms());
        await requests.ConfirmAgreementAsync(agreementId, ownerId, 1);

        interceptor.FailNext = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => requests.ConfirmAgreementAsync(agreementId, tenantId, 1));

        using var scope = site.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RentAgencyDbContext>();

        var listing = await db.Listings.AsNoTracking().SingleAsync(l => l.Id == listingId);
        var confirmations = await db.RentalAgreements
            .AsNoTracking()
            .Where(a => a.Id == agreementId)
            .SelectMany(a => a.Confirmations)
            .ToListAsync();

        Assert.Equal(ListingStatus.Published, listing.Status);
        Assert.Single(confirmations);
        Assert.Equal(ownerId, confirmations[0].ParticipantId);
    }
}

public class CompletionAndReviewTests
{
    private static CreateRentalRequest Request() => new(
        "Хочу снять.",
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
        DateOnly.FromDateTime(DateTime.UtcNow.AddDays(35)));

    private static async Task<(SprintTestSite Site, Guid OwnerId, Guid TenantId, Guid AgreementId)>
        ArrangeConfirmedAsync()
    {
        var site = new SprintTestSite();
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var (tenantId, _) = await site.CreateUserAsync(UserRoles.Tenant);
        var listingId = await site.PublishListingAsync(ownerId);
        var requests = site.Get<IRequestService>();
        var requestId = await requests.SubmitAsync(tenantId, listingId, Request());

        var agreementId = await requests.ProposeAgreementAsync(requestId, ownerId, new RentalTerms(
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(35)),
            40_000m,
            0m));

        await requests.ConfirmAgreementAsync(agreementId, ownerId, 1);
        await requests.ConfirmAgreementAsync(agreementId, tenantId, 1);

        return (site, ownerId, tenantId, agreementId);
    }

    [Fact]
    public async Task Completion_RequiresBothParticipantsAndIsAtomic()
    {
        var (site, ownerId, tenantId, agreementId) = await ArrangeConfirmedAsync();
        using var _site = site;
        var rentals = site.Get<IRentalService>();

        await rentals.ConfirmCompletionAsync(agreementId, tenantId);
        var afterFirst = await rentals.GetAgreementAsync(agreementId, tenantId);
        Assert.Equal(AgreementStatus.Confirmed, afterFirst.Status);
        Assert.Equal([tenantId], afterFirst.CompletionConfirmedBy);

        var (outsiderId, _) = await site.CreateUserAsync(UserRoles.Tenant);
        await Assert.ThrowsAsync<ForbiddenException>(
            () => rentals.ConfirmCompletionAsync(agreementId, outsiderId));

        await rentals.ConfirmCompletionAsync(agreementId, ownerId);
        var afterSecond = await rentals.GetAgreementAsync(agreementId, ownerId);
        Assert.Equal(AgreementStatus.Completed, afterSecond.Status);
        Assert.NotNull(afterSecond.CompletedAt);
    }

    [Fact]
    public async Task Review_RequiresCompletedRentalAndOtherParty()
    {
        var (site, ownerId, tenantId, agreementId) = await ArrangeConfirmedAsync();
        using var _site = site;
        var rentals = site.Get<IRentalService>();
        var (outsiderId, _) = await site.CreateUserAsync(UserRoles.Tenant);

        await Assert.ThrowsAsync<ConflictException>(
            () => rentals.LeaveReviewAsync(agreementId, tenantId, ownerId, "Рано"));

        await rentals.ConfirmCompletionAsync(agreementId, tenantId);
        await rentals.ConfirmCompletionAsync(agreementId, ownerId);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => rentals.LeaveReviewAsync(agreementId, outsiderId, ownerId, "Чужой"));
        await Assert.ThrowsAsync<ValidationException>(
            () => rentals.LeaveReviewAsync(agreementId, tenantId, outsiderId, "Не та сторона"));

        var review = await rentals.LeaveReviewAsync(agreementId, tenantId, ownerId, "Ответственный арендодатель.");
        var repeat = await rentals.LeaveReviewAsync(agreementId, tenantId, ownerId, "Повтор.");

        Assert.Equal(review.Id, repeat.Id);
        Assert.Equal(
            1,
            await site.Get<RentAgencyDbContext>().Reviews.CountAsync(r => r.AgreementId == agreementId && r.AuthorId == tenantId));

        var ownerReview = await rentals.LeaveReviewAsync(agreementId, ownerId, tenantId, "Аккуратный арендатор.");
        Assert.Equal(2, await site.Get<RentAgencyDbContext>().Reviews.CountAsync(r => r.AgreementId == agreementId));
        Assert.Equal(tenantId, ownerReview.RecipientId);
    }
}
