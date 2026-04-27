namespace CentralAuthNotificationPlatform.Dtos;

public sealed record AuditLogDto(
    Guid Id,
    string Action,
    string? AppName,
    string? ExternalUserId,
    string? PlatformEmail,
    DateTimeOffset CreatedAt);

public sealed record AuditLogListResponse(IReadOnlyList<AuditLogDto> Items);
