using System.Net;
using System.Net.Mail;
using CentralAuthNotificationPlatform.Models;
using CentralAuthNotificationPlatform.Options;
using Microsoft.Extensions.Options;

namespace CentralAuthNotificationPlatform.Services;

public interface IAppEmailSender
{
    Task SendPasswordResetAsync(
        ApplicationUser user,
        ExternalApp? externalApp,
        string resetUrl,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken);
}

public sealed class SmtpEmailSender(
    IOptions<SmtpOptions> smtpOptions,
    ILogger<SmtpEmailSender> logger) : IAppEmailSender
{
    private readonly SmtpOptions _options = smtpOptions.Value;

    public async Task SendPasswordResetAsync(
        ApplicationUser user,
        ExternalApp? externalApp,
        string resetUrl,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            logger.LogInformation("SMTP host is not configured. Password reset email was skipped.");
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = "Reset your password",
            Body = BuildBody(user, externalApp, resetUrl, expiresAt),
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(user.Email ?? string.Empty, user.DisplayName));

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.UserName))
        {
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }

    private static string BuildBody(ApplicationUser user, ExternalApp? externalApp, string resetUrl, DateTimeOffset expiresAt)
    {
        var source = externalApp is null ? "Central Auth Hub" : externalApp.Name;
        return $"""
        Hello {user.DisplayName},

        {source} requested a password reset for your account.

        Reset link:
        {resetUrl}

        This link expires at {expiresAt:yyyy-MM-dd HH:mm} UTC.

        If you did not request this reset, ignore this message.
        """;
    }
}
