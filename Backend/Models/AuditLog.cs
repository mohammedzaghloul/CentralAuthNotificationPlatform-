namespace CentralAuthNotificationPlatform.Models;

public static class AuditActions
{
    public const string AccountLinked = "ACCOUNT_LINKED";
    public const string AccountUnlinked = "ACCOUNT_UNLINKED";
    public const string ForgotPasswordRequested = "FORGOT_PASSWORD_REQUESTED";
    public const string ForgotPasswordRateLimited = "FORGOT_PASSWORD_RATE_LIMITED";
}

public sealed class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Action { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public Guid? ExternalAppId { get; set; }
    public ExternalApp? ExternalApp { get; set; }
    public string? AppName { get; set; }
    public string? ExternalUserId { get; set; }
    public string? PlatformEmail { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? MetadataJson { get; set; }
}
