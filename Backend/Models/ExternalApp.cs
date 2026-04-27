namespace CentralAuthNotificationPlatform.Models;

public sealed class ExternalApp
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }
    public ApplicationUser? OwnerUser { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecretHash { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string ApiKeyHash { get; set; } = string.Empty;
    public string ApiKeyPreview { get; set; } = string.Empty;
    public bool IsApiKeyActive { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAt { get; set; }

    public ICollection<UserApp> UserApps { get; set; } = new List<UserApp>();
    public ICollection<UserLink> UserLinks { get; set; } = new List<UserLink>();
    public ICollection<OAuthAuthorizationCode> AuthorizationCodes { get; set; } = new List<OAuthAuthorizationCode>();
}
