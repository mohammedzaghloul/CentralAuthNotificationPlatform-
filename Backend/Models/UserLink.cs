namespace CentralAuthNotificationPlatform.Models;

public sealed class UserLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExternalAppId { get; set; }
    public ExternalApp? ExternalApp { get; set; }
    public string ExternalUserId { get; set; } = string.Empty;
    public string NormalizedExternalUserId { get; set; } = string.Empty;
    public Guid PlatformUserId { get; set; }
    public ApplicationUser? PlatformUser { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
