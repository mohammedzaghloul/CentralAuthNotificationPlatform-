namespace CentralAuthNotificationPlatform.Options;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "CentralAuthNotificationPlatform";
    public string Audience { get; set; } = "CentralAuthNotificationPlatform.Clients";
    public string SigningKey { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
    public int OAuthAccessTokenMinutes { get; set; } = 10;
    public int AuthorizationCodeMinutes { get; set; } = 5;
    public int PasswordResetMinutes { get; set; } = 10;
}
