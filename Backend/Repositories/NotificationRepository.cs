using CentralAuthNotificationPlatform.Data;
using CentralAuthNotificationPlatform.Models;
using Microsoft.EntityFrameworkCore;

namespace CentralAuthNotificationPlatform.Repositories;

public sealed class NotificationRepository(AuthHubDbContext dbContext) : INotificationRepository
{
    public async Task<IReadOnlyList<Notification>> ListForUserAsync(Guid userId, int take, CancellationToken cancellationToken)
    {
        return await dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken)
    {
        return dbContext.Notifications.CountAsync(notification => notification.UserId == userId && !notification.IsRead, cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>> FindUnreadAsync(Guid userId, IReadOnlyList<Guid> notificationIds, CancellationToken cancellationToken)
    {
        return await dbContext.Notifications
            .Where(notification => notification.UserId == userId && !notification.IsRead && notificationIds.Contains(notification.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Notification notification, CancellationToken cancellationToken)
    {
        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
