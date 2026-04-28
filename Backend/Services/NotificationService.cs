using CentralAuthNotificationPlatform.Dtos;
using CentralAuthNotificationPlatform.Models;
using CentralAuthNotificationPlatform.Repositories;
using System.Text.Json;

namespace CentralAuthNotificationPlatform.Services;

public interface INotificationService
{
    Task<NotificationListResponse> GetInboxAsync(Guid userId, CancellationToken cancellationToken);
    Task<int> MarkAsReadAsync(Guid userId, IReadOnlyList<Guid> notificationIds, CancellationToken cancellationToken);
    Task CreateAsync(Notification notification, CancellationToken cancellationToken);
}

public sealed class NotificationService(INotificationRepository notificationRepository) : INotificationService
{
    public async Task<NotificationListResponse> GetInboxAsync(Guid userId, CancellationToken cancellationToken)
    {
        var notifications = await notificationRepository.ListForUserAsync(userId, 100, cancellationToken);
        var visibleNotifications = KeepLatestActiveResetNotifications(notifications);

        return new NotificationListResponse(
            visibleNotifications.Select(ToDto).ToList(),
            visibleNotifications.Count(notification => !notification.IsRead));
    }

    public async Task<int> MarkAsReadAsync(Guid userId, IReadOnlyList<Guid> notificationIds, CancellationToken cancellationToken)
    {
        if (notificationIds.Count == 0)
        {
            return 0;
        }

        var notifications = await notificationRepository.FindUnreadAsync(userId, notificationIds, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        await notificationRepository.SaveChangesAsync(cancellationToken);
        return notifications.Count;
    }

    public Task CreateAsync(Notification notification, CancellationToken cancellationToken)
    {
        return notificationRepository.AddAsync(notification, cancellationToken);
    }

    private static IReadOnlyList<Notification> KeepLatestActiveResetNotifications(IReadOnlyList<Notification> notifications)
    {
        var now = DateTimeOffset.UtcNow;
        var output = new List<Notification>();
        var passwordResetKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var notification in notifications)
        {
            if (!string.Equals(notification.Type, NotificationTypes.PasswordReset, StringComparison.Ordinal))
            {
                output.Add(notification);
                continue;
            }

            var resetInfo = TryReadResetMetadata(notification.MetadataJson);
            if (resetInfo.ExpiresAt is not null && resetInfo.ExpiresAt <= now)
            {
                continue;
            }

            var key = $"{resetInfo.ExternalAppId?.ToString() ?? notification.SourceAppName ?? "platform"}:{resetInfo.ExternalUserId ?? notification.UserId.ToString()}";
            if (passwordResetKeys.Add(key))
            {
                output.Add(notification);
            }
        }

        return output;
    }

    private static ResetNotificationMetadata TryReadResetMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return new ResetNotificationMetadata(null, null, null, null);
        }

        try
        {
            using var json = JsonDocument.Parse(metadataJson);
            var root = json.RootElement;

            DateTimeOffset? expiresAt = null;
            Guid? externalAppId = null;
            string? externalUserId = null;
            string? resetUrl = null;

            if (root.TryGetProperty("expiresAt", out var expiresAtProperty) &&
                expiresAtProperty.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(expiresAtProperty.GetString(), out var parsedExpiresAt))
            {
                expiresAt = parsedExpiresAt;
            }

            if (root.TryGetProperty("externalAppId", out var externalAppProperty) &&
                externalAppProperty.ValueKind == JsonValueKind.String &&
                Guid.TryParse(externalAppProperty.GetString(), out var parsedExternalAppId))
            {
                externalAppId = parsedExternalAppId;
            }

            if (root.TryGetProperty("externalUserId", out var externalUserIdProperty) &&
                externalUserIdProperty.ValueKind == JsonValueKind.String)
            {
                externalUserId = externalUserIdProperty.GetString();
            }
            else if (root.TryGetProperty("externalEmail", out var externalEmailProperty) &&
                externalEmailProperty.ValueKind == JsonValueKind.String)
            {
                externalUserId = externalEmailProperty.GetString();
            }

            if (root.TryGetProperty("resetUrl", out var resetUrlProperty) &&
                resetUrlProperty.ValueKind == JsonValueKind.String)
            {
                resetUrl = resetUrlProperty.GetString();
            }

            return new ResetNotificationMetadata(expiresAt, externalAppId, externalUserId, resetUrl);
        }
        catch (JsonException)
        {
            return new ResetNotificationMetadata(null, null, null, null);
        }
    }

    private static NotificationDto ToDto(Notification notification)
    {
        var isPasswordReset = string.Equals(notification.Type, NotificationTypes.PasswordReset, StringComparison.Ordinal);
        var resetInfo = isPasswordReset
            ? TryReadResetMetadata(notification.MetadataJson)
            : new ResetNotificationMetadata(null, null, null, null);

        var actionLabel = string.IsNullOrWhiteSpace(resetInfo.ResetUrl)
            ? null
            : "فتح رابط الاسترجاع";
        var sourceAppName = notification.SourceAppName ?? "منصة الدخول الموحد";
        var title = notification.Title;
        var message = notification.Message;

        if (isPasswordReset)
        {
            var expiresText = resetInfo.ExpiresAt is null
                ? string.Empty
                : $" ينتهي الرابط في {resetInfo.ExpiresAt:yyyy-MM-dd HH:mm} UTC.";

            title = $"طلب استرجاع كلمة المرور من {sourceAppName}";
            message = $"تم إنشاء طلب استرجاع كلمة المرور من {sourceAppName}. افتح رابط الاسترجاع من هذا الإشعار لتعيين كلمة مرور جديدة.{expiresText}";
        }

        return new NotificationDto(
            notification.Id,
            notification.Type,
            title,
            message,
            notification.SourceAppName,
            notification.IsRead,
            notification.CreatedAt,
            notification.ReadAt,
            resetInfo.ResetUrl,
            actionLabel);
    }

    private sealed record ResetNotificationMetadata(DateTimeOffset? ExpiresAt, Guid? ExternalAppId, string? ExternalUserId, string? ResetUrl);
}
