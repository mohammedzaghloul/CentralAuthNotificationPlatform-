using CentralAuthNotificationPlatform.Data;
using CentralAuthNotificationPlatform.Dtos;
using CentralAuthNotificationPlatform.Models;
using Microsoft.EntityFrameworkCore;

namespace CentralAuthNotificationPlatform.Services;

public interface IAuditLogService
{
    Task LogAsync(AuditLog auditLog, CancellationToken cancellationToken);
    Task<AuditLogListResponse> GetForUserAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed class AuditLogService(AuthHubDbContext dbContext) : IAuditLogService
{
    public async Task LogAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(auditLog);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuditLogListResponse> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var logs = await dbContext.AuditLogs
            .AsNoTracking()
            .Where(log =>
                log.UserId == userId ||
                (log.ExternalApp != null && log.ExternalApp.OwnerUserId == userId))
            .Include(log => log.ExternalApp)
            .OrderByDescending(log => log.CreatedAt)
            .Take(100)
            .Select(log => new AuditLogDto(
                log.Id,
                log.Action,
                log.AppName ?? log.ExternalApp!.Name,
                log.ExternalUserId,
                log.PlatformEmail,
                log.CreatedAt))
            .ToListAsync(cancellationToken);

        return new AuditLogListResponse(logs);
    }
}
