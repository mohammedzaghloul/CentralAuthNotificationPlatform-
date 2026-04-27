using System.ComponentModel.DataAnnotations;

namespace CentralAuthNotificationPlatform.Dtos;

public sealed record CreateUserLinkRequest(
    [Required] Guid ExternalAppId,
    [MaxLength(256)] string? ExternalUserId,
    [EmailAddress, MaxLength(256)] string? ExternalEmail = null,
    [EmailAddress, MaxLength(256)] string? PlatformEmail = null);

public sealed record CreateIntegrationUserLinkRequest(
    [MaxLength(256)] string? ExternalUserId,
    [EmailAddress, MaxLength(256)] string? ExternalEmail = null)
{
    public string UserIdentifier =>
        string.IsNullOrWhiteSpace(ExternalUserId)
            ? ExternalEmail?.Trim() ?? string.Empty
            : ExternalUserId.Trim();
}

public sealed record UserLinkStatusDto(
    bool IsLinked,
    string? ExternalAppName,
    string? ExternalUserId,
    string? PlatformEmail,
    string? PlatformDisplayName,
    DateTimeOffset? LinkedAt);

public sealed record UserLinkDto(
    Guid Id,
    Guid ExternalAppId,
    string ExternalAppName,
    string ExternalUserId,
    Guid PlatformUserId,
    string PlatformEmail,
    string PlatformDisplayName,
    DateTimeOffset CreatedAt);
