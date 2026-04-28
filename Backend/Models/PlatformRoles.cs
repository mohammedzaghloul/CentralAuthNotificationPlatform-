namespace CentralAuthNotificationPlatform.Models;

public static class PlatformRoles
{
    public const string User = "User";
    public const string Developer = "Developer";
    public const string Admin = "Admin";

    public static readonly IReadOnlySet<string> AllowedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        User,
        Developer,
        Admin
    };

    public static string Normalize(string? role)
    {
        if (string.Equals(role, Admin, StringComparison.OrdinalIgnoreCase))
        {
            return Admin;
        }

        return string.Equals(role, Developer, StringComparison.OrdinalIgnoreCase) ? Developer : User;
    }
}
