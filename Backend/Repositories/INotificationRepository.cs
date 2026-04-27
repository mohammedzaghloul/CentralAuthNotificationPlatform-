using CentralAuthNotificationPlatform.Models;

namespace CentralAuthNotificationPlatform.Repositories;

public interface INotificationRepository
{
    Task<IReadOnlyList<Notification>> ListForUserAsync(Guid userId, int take, CancellationToken cancellationToken);
    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Notification>> FindUnreadAsync(Guid userId, IReadOnlyList<Guid> notificationIds, CancellationToken cancellationToken);
    Task AddAsync(Notification notification, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
