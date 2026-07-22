namespace CPMS.BLL.Notifications;

public sealed record NotificationListResult(
    IReadOnlyList<NotificationItem> Items,
    int UnreadCount);

public interface INotificationService
{
    Task<NotificationListResult> GetLatestAsync(
        int recipientId, int take, CancellationToken cancellationToken);

    Task<bool> MarkReadAsync(
        int recipientId, int notificationId, CancellationToken cancellationToken);

    Task<int> MarkAllReadAsync(int recipientId, CancellationToken cancellationToken);
}

public sealed class NotificationService(INotificationStore store) : INotificationService
{
    private const int DefaultTake = 20;
    private const int MaximumTake = 100;

    public async Task<NotificationListResult> GetLatestAsync(
        int recipientId, int take, CancellationToken cancellationToken)
    {
        ValidateRecipient(recipientId);
        int normalizedTake = take <= 0 ? DefaultTake : Math.Min(take, MaximumTake);
        IReadOnlyList<NotificationItem> items = await store.GetLatestAsync(
            recipientId, normalizedTake, cancellationToken);
        int unreadCount = await store.CountUnreadAsync(recipientId, cancellationToken);
        return new NotificationListResult(items, unreadCount);
    }

    public Task<bool> MarkReadAsync(
        int recipientId, int notificationId, CancellationToken cancellationToken)
    {
        ValidateRecipient(recipientId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(notificationId);
        return store.MarkReadAsync(recipientId, notificationId, DateTime.UtcNow, cancellationToken);
    }

    public Task<int> MarkAllReadAsync(int recipientId, CancellationToken cancellationToken)
    {
        ValidateRecipient(recipientId);
        return store.MarkAllReadAsync(recipientId, DateTime.UtcNow, cancellationToken);
    }

    private static void ValidateRecipient(int recipientId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(recipientId);
    }
}
