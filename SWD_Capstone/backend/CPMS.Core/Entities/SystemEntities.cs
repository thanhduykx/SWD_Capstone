using CPMS.Core.Enums;

namespace CPMS.Core.Entities;

public sealed class AuditLog
{
    public long Id { get; set; }
    public int? UserId { get; set; }
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public int EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Notification
{
    public int Id { get; set; }
    public int RecipientId { get; set; }
    public int? SenderId { get; set; }
    public NotificationType Type { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
