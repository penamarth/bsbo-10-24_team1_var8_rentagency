using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentAgency.Tests.Infrastructure;
using RentAgency.Web.Data;
using RentAgency.Web.Features.Accounts;
using RentAgency.Web.Features.Complaints.Application;
using RentAgency.Web.Features.Complaints.Domain;
using RentAgency.Web.Infrastructure;
using RentAgency.Web.Infrastructure.Files;

namespace RentAgency.Tests.Complaints;

public class ComplaintServiceTests
{
    [Fact]
    public async Task Submit_ListingOrUserTargetWithoutRental()
    {
        using var site = new SprintTestSite();
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var (reporterId, _) = await site.CreateUserAsync(UserRoles.Tenant);
        var listingId = await site.PublishListingAsync(ownerId);
        var complaints = site.Get<IComplaintService>();

        var listingComplaintId = await complaints.SubmitAsync(
            reporterId,
            new ComplaintTarget(listingId, null),
            "Объявление не соответствует описанию.",
            []);

        var userComplaintId = await complaints.SubmitAsync(
            reporterId,
            new ComplaintTarget(null, ownerId),
            "Некорректное общение.",
            []);

        var db = site.Get<RentAgencyDbContext>();
        Assert.Equal(2, await db.Complaints.CountAsync());
        Assert.Equal(ComplaintStatus.Open, await db.Complaints
            .Where(c => c.Id == listingComplaintId).Select(c => c.Status).SingleAsync());
        Assert.NotNull(await db.Complaints.FindAsync(userComplaintId));
    }

    [Fact]
    public async Task Submit_RequiresExactlyOneExistingTarget()
    {
        using var site = new SprintTestSite();
        var (ownerId, _) = await site.CreateUserAsync(UserRoles.Owner);
        var (reporterId, _) = await site.CreateUserAsync(UserRoles.Tenant);
        var listingId = await site.PublishListingAsync(ownerId);
        var complaints = site.Get<IComplaintService>();

        await Assert.ThrowsAsync<ValidationException>(() => complaints.SubmitAsync(
            reporterId, new ComplaintTarget(null, null), "Нет цели", []));

        await Assert.ThrowsAsync<ValidationException>(() => complaints.SubmitAsync(
            reporterId, new ComplaintTarget(listingId, ownerId), "Две цели", []));

        await Assert.ThrowsAsync<ValidationException>(() => complaints.SubmitAsync(
            reporterId, new ComplaintTarget(listingId, null), " ", []));

        await Assert.ThrowsAsync<NotFoundException>(() => complaints.SubmitAsync(
            reporterId, new ComplaintTarget(Guid.NewGuid(), null), "Нет объявления", []));
    }

    [Fact]
    public async Task Access_OnlyReporterAndModerator()
    {
        using var site = new SprintTestSite();
        var (reporterId, _) = await site.CreateUserAsync(UserRoles.Tenant);
        var (outsiderId, _) = await site.CreateUserAsync(UserRoles.Tenant);
        var moderatorId = await site.ModeratorIdAsync();
        var complaints = site.Get<IComplaintService>();
        var storage = site.Get<IFileStorage>();

        var stored = await storage.SaveAsync(
            UploadFixtures.AsStream(UploadFixtures.Pdf()),
            UploadKind.ComplaintEvidence,
            "application/pdf",
            "evidence.pdf");

        var complaintId = await complaints.SubmitAsync(
            reporterId,
            new ComplaintTarget(null, outsiderId),
            "Жалоба с доказательством.",
            [new UploadInput(stored.StorageKey, stored.OriginalFileName, stored.ContentType)]);

        var details = await complaints.GetAsync(complaintId, reporterId);
        var evidenceId = Assert.Single(details.Evidence).Id;

        await Assert.ThrowsAsync<ForbiddenException>(() => complaints.GetAsync(complaintId, outsiderId));
        await Assert.ThrowsAsync<ForbiddenException>(
            () => complaints.OpenEvidenceAsync(complaintId, evidenceId, outsiderId));

        await using var reporterStream = await complaints.OpenEvidenceAsync(complaintId, evidenceId, reporterId);
        Assert.NotNull(reporterStream);
        await using var moderatorStream = await complaints.OpenEvidenceAsync(complaintId, evidenceId, moderatorId);
        Assert.NotNull(moderatorStream);

        var queue = await complaints.GetForModeratorAsync(moderatorId);
        Assert.Contains(queue, c => c.Id == complaintId);
    }

    [Fact]
    public async Task RequestDetails_KeepsOpenAndResolveRecordsDecision()
    {
        using var site = new SprintTestSite();
        var (reporterId, _) = await site.CreateUserAsync(UserRoles.Tenant);
        var (targetId, _) = await site.CreateUserAsync(UserRoles.Tenant);
        var moderatorId = await site.ModeratorIdAsync();
        var complaints = site.Get<IComplaintService>();

        var complaintId = await complaints.SubmitAsync(
            reporterId, new ComplaintTarget(null, targetId), "Описание проблемы.", []);

        await complaints.RequestDetailsAsync(moderatorId, complaintId, "Пришлите переписку.");

        var open = await complaints.GetAsync(complaintId, moderatorId);
        Assert.Equal(ComplaintStatus.Open, open.Status);
        Assert.Single(open.History);
        Assert.Equal("Пришлите переписку.", open.History[0].RequestedDetails);

        await complaints.ResolveAsync(moderatorId, complaintId, "Нарушений нет", "Проверено.");

        var resolved = await complaints.GetAsync(complaintId, reporterId);
        Assert.Equal(ComplaintStatus.Resolved, resolved.Status);
        Assert.Equal("Нарушений нет", resolved.History.Last().Decision);
        Assert.Equal("Проверено.", resolved.History.Last().Rationale);

        var notifications = await site.Get<INotificationService>().GetForUserAsync(reporterId);
        Assert.Single(notifications);
    }

    [Fact]
    public async Task FailedComplaintSave_RemovesUploadedEvidence()
    {
        var interceptor = new FailNextSaveInterceptor();
        using var site = new SprintTestSite(interceptor);
        var (reporterId, _) = await site.CreateUserAsync(UserRoles.Tenant);
        var (targetId, _) = await site.CreateUserAsync(UserRoles.Tenant);
        var complaints = site.Get<IComplaintService>();
        var storage = site.Get<IFileStorage>();

        var stored = await storage.SaveAsync(
            UploadFixtures.AsStream(UploadFixtures.Pdf()),
            UploadKind.ComplaintEvidence,
            "application/pdf",
            "evidence.pdf");

        interceptor.FailNext = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => complaints.SubmitAsync(
            reporterId,
            new ComplaintTarget(null, targetId),
            "Жалоба не сохранится.",
            [new UploadInput(stored.StorageKey, stored.OriginalFileName, stored.ContentType)]));

        Assert.Null(await storage.OpenReadAsync(stored.StorageKey));

        using var scope = site.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RentAgencyDbContext>();
        Assert.Equal(0, await db.Complaints.CountAsync());
        Assert.Equal(0, await db.Set<ComplaintEvidence>().CountAsync());
    }
}
