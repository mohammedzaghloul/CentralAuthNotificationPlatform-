namespace CentralAuthNotificationPlatform.Dtos;

public sealed record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string Message,
    string? SourceAppName,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt,
    string? ActionUrl,
    string? ActionLabel);

public sealed record NotificationListResponse(
    IReadOnlyList<NotificationDto> Items,
    int UnreadCount);

public sealed record MarkNotificationsReadRequest(IReadOnlyList<Guid> NotificationIds);
