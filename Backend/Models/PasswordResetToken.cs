namespace CentralAuthNotificationPlatform.Models;

public sealed class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public Guid? ExternalAppId { get; set; }
    public ExternalApp? ExternalApp { get; set; }
    public string? ExternalUserId { get; set; }
    public string? NormalizedExternalUserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public bool IsUsed { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}
