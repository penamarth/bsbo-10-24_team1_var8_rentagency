using Microsoft.EntityFrameworkCore;
using RentAgency.Web.Data;

namespace RentAgency.Web.Features.Accounts;

public interface INotificationService
{
    Task NotifyAsync(Guid userId, string text, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Notification>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class NotificationService(RentAgencyDbContext db) : INotificationService
{
    public async Task NotifyAsync(Guid userId, string text, CancellationToken cancellationToken = default)
    {
        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Text = text,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
