using CPMS.BLL.Enums;

namespace CPMS.BLL.Notifications;

public sealed record NotificationItem(
    int Id,
    NotificationType Type,
    string Title,
    string Message,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt);

public interface INotificationStore
{
    Task<IReadOnlyList<NotificationItem>> GetLatestAsync(
        int recipientId, int take, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(int recipientId, CancellationToken cancellationToken);

    Task<bool> MarkReadAsync(
        int recipientId, int notificationId, DateTime readAt, CancellationToken cancellationToken);

    Task<int> MarkAllReadAsync(
        int recipientId, DateTime readAt, CancellationToken cancellationToken);
}
