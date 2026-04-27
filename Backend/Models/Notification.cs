namespace CentralAuthNotificationPlatform.Models;

public static class NotificationTypes
{
    public const string System = "SYSTEM";
    public const string PasswordReset = "PASSWORD_RESET";
}

public sealed class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public string Type { get; set; } = NotificationTypes.System;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? SourceAppName { get; set; }
    public string? MetadataJson { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
}
