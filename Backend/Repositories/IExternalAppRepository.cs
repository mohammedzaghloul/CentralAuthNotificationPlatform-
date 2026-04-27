using CentralAuthNotificationPlatform.Models;

namespace CentralAuthNotificationPlatform.Repositories;

public interface IExternalAppRepository
{
    Task<IReadOnlyList<ExternalApp>> ListOwnedAsync(Guid ownerUserId, CancellationToken cancellationToken);
    Task<ExternalApp?> FindByIdAsync(Guid appId, CancellationToken cancellationToken);
    Task<ExternalApp?> FindByClientIdAsync(string clientId, CancellationToken cancellationToken);
    Task<ExternalApp?> FindByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken);
    Task<bool> ExistsForOwnerAsync(Guid ownerUserId, string name, CancellationToken cancellationToken);
    Task AddAsync(ExternalApp app, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
