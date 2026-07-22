using CPMS.BLL.Enums;
using CPMS.BLL.Notifications;

namespace CPMS.UnitTests.Services;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task GetLatest_IsRecipientScopedAndCapsRequestedPageSize()
    {
        var store = new RecordingNotificationStore
        {
            Items = [new NotificationItem(7, NotificationType.ReviewSchedulePublished,
                "Lịch review", "Đã công bố", false, DateTime.UtcNow, null)],
            UnreadCount = 1
        };
        var service = new NotificationService(store);

        NotificationListResult result = await service.GetLatestAsync(24, 500, CancellationToken.None);

        Assert.Equal(24, store.LastRecipientId);
        Assert.Equal(100, store.LastTake);
        Assert.Single(result.Items);
        Assert.Equal(1, result.UnreadCount);
    }

    [Fact]
    public async Task MarkRead_UsesAuthenticatedRecipientId()
    {
        var store = new RecordingNotificationStore { MarkReadResult = true };
        var service = new NotificationService(store);

        bool result = await service.MarkReadAsync(24, 7, CancellationToken.None);

        Assert.True(result);
        Assert.Equal(24, store.LastRecipientId);
        Assert.Equal(7, store.LastNotificationId);
    }

    private sealed class RecordingNotificationStore : INotificationStore
    {
        public IReadOnlyList<NotificationItem> Items { get; init; } = [];
        public int UnreadCount { get; init; }
        public bool MarkReadResult { get; init; }
        public int LastRecipientId { get; private set; }
        public int LastNotificationId { get; private set; }
        public int LastTake { get; private set; }

        public Task<IReadOnlyList<NotificationItem>> GetLatestAsync(
            int recipientId, int take, CancellationToken cancellationToken)
        {
            LastRecipientId = recipientId;
            LastTake = take;
            return Task.FromResult(Items);
        }

        public Task<int> CountUnreadAsync(int recipientId, CancellationToken cancellationToken)
        {
            LastRecipientId = recipientId;
            return Task.FromResult(UnreadCount);
        }

        public Task<bool> MarkReadAsync(
            int recipientId, int notificationId, DateTime readAt, CancellationToken cancellationToken)
        {
            LastRecipientId = recipientId;
            LastNotificationId = notificationId;
            return Task.FromResult(MarkReadResult);
        }

        public Task<int> MarkAllReadAsync(
            int recipientId, DateTime readAt, CancellationToken cancellationToken)
        {
            LastRecipientId = recipientId;
            return Task.FromResult(UnreadCount);
        }
    }
}
