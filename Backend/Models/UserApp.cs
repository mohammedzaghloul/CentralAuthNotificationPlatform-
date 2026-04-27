namespace CentralAuthNotificationPlatform.Models;

public sealed class UserApp
{
    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public Guid ExternalAppId { get; set; }
    public ExternalApp? ExternalApp { get; set; }
    public DateTimeOffset ConsentedAt { get; set; } = DateTimeOffset.UtcNow;
}
