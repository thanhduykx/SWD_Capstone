using CPMS.BLL.Notifications;
using CPMS.DAL.Data;
using CPMS.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace CPMS.DAL.Repositories;

public sealed class NotificationStore(CpmsDbContext dbContext) : INotificationStore
{
    public async Task<IReadOnlyList<NotificationItem>> GetLatestAsync(
        int recipientId, int take, CancellationToken cancellationToken) =>
        await dbContext.Notifications
            .AsNoTracking()
            .Where(x => x.RecipientId == recipientId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .Select(x => new NotificationItem(
                x.Id, x.Type, x.Title, x.Message, x.IsRead, x.CreatedAt, x.ReadAt))
            .ToListAsync(cancellationToken);

    public Task<int> CountUnreadAsync(int recipientId, CancellationToken cancellationToken) =>
        dbContext.Notifications.CountAsync(
            x => x.RecipientId == recipientId && !x.IsRead, cancellationToken);

    public async Task<bool> MarkReadAsync(
        int recipientId, int notificationId, DateTime readAt, CancellationToken cancellationToken)
    {
        Notification? notification = await dbContext.Notifications.SingleOrDefaultAsync(
            x => x.Id == notificationId && x.RecipientId == recipientId, cancellationToken);
        if (notification is null)
        {
            return false;
        }
        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = readAt;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return true;
    }

    public async Task<int> MarkAllReadAsync(
        int recipientId, DateTime readAt, CancellationToken cancellationToken) =>
        await dbContext.Notifications
            .Where(x => x.RecipientId == recipientId && !x.IsRead)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.IsRead, true)
                .SetProperty(x => x.ReadAt, readAt), cancellationToken);
}
