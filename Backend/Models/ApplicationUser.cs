using Microsoft.AspNetCore.Identity;

namespace CentralAuthNotificationPlatform.Models;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<ExternalApp> OwnedApps { get; set; } = new List<ExternalApp>();
    public ICollection<UserApp> UserApps { get; set; } = new List<UserApp>();
    public ICollection<UserLink> UserLinks { get; set; } = new List<UserLink>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
}
