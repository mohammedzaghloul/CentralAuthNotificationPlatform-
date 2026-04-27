using CentralAuthNotificationPlatform.Data;
using CentralAuthNotificationPlatform.Models;
using Microsoft.EntityFrameworkCore;

namespace CentralAuthNotificationPlatform.Repositories;

public sealed class OAuthRepository(AuthHubDbContext dbContext) : IOAuthRepository
{
    public async Task AddAuthorizationCodeAsync(OAuthAuthorizationCode code, CancellationToken cancellationToken)
    {
        dbContext.OAuthAuthorizationCodes.Add(code);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<OAuthAuthorizationCode?> FindActiveAuthorizationCodeAsync(string codeHash, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return dbContext.OAuthAuthorizationCodes
            .Include(code => code.User)
            .Include(code => code.ExternalApp)
            .FirstOrDefaultAsync(code => code.CodeHash == codeHash && !code.IsUsed && code.ExpiresAt > now, cancellationToken);
    }

    public async Task EnsureUserAppAsync(Guid userId, Guid externalAppId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.UserApps
            .AnyAsync(userApp => userApp.UserId == userId && userApp.ExternalAppId == externalAppId, cancellationToken);

        if (!exists)
        {
            dbContext.UserApps.Add(new UserApp
            {
                UserId = userId,
                ExternalAppId = externalAppId
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
