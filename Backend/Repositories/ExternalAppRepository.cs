using CentralAuthNotificationPlatform.Data;
using CentralAuthNotificationPlatform.Models;
using Microsoft.EntityFrameworkCore;

namespace CentralAuthNotificationPlatform.Repositories;

public sealed class ExternalAppRepository(AuthHubDbContext dbContext) : IExternalAppRepository
{
    public async Task<IReadOnlyList<ExternalApp>> ListOwnedAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        return await dbContext.ExternalApps
            .AsNoTracking()
            .Where(app => app.OwnerUserId == ownerUserId)
            .OrderBy(app => app.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<ExternalApp?> FindByIdAsync(Guid appId, CancellationToken cancellationToken)
    {
        return dbContext.ExternalApps.FirstOrDefaultAsync(app => app.Id == appId, cancellationToken);
    }

    public Task<ExternalApp?> FindByClientIdAsync(string clientId, CancellationToken cancellationToken)
    {
        return dbContext.ExternalApps.FirstOrDefaultAsync(app => app.ClientId == clientId && app.IsActive, cancellationToken);
    }

    public Task<ExternalApp?> FindByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken)
    {
        return dbContext.ExternalApps.FirstOrDefaultAsync(app => app.ApiKeyHash == apiKeyHash && app.IsActive, cancellationToken);
    }

    public Task<bool> ExistsForOwnerAsync(Guid ownerUserId, string name, CancellationToken cancellationToken)
    {
        return dbContext.ExternalApps.AnyAsync(app => app.OwnerUserId == ownerUserId && app.Name == name, cancellationToken);
    }

    public async Task AddAsync(ExternalApp app, CancellationToken cancellationToken)
    {
        dbContext.ExternalApps.Add(app);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
