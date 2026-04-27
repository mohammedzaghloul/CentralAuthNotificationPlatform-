namespace CentralAuthNotificationPlatform.Models;

public static class PlatformRoles
{
    public const string User = "User";
    public const string Developer = "Developer";

    public static readonly IReadOnlySet<string> AllowedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        User,
        Developer
    };

    public static string Normalize(string? role)
    {
        return string.Equals(role, Developer, StringComparison.OrdinalIgnoreCase)
            ? Developer
            : User;
    }
}
