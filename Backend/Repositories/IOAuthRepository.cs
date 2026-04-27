using CentralAuthNotificationPlatform.Models;

namespace CentralAuthNotificationPlatform.Repositories;

public interface IOAuthRepository
{
    Task AddAuthorizationCodeAsync(OAuthAuthorizationCode code, CancellationToken cancellationToken);
    Task<OAuthAuthorizationCode?> FindActiveAuthorizationCodeAsync(string codeHash, CancellationToken cancellationToken);
    Task EnsureUserAppAsync(Guid userId, Guid externalAppId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
