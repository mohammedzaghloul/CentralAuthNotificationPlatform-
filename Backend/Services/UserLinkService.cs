using CentralAuthNotificationPlatform.Data;
using CentralAuthNotificationPlatform.Dtos;
using CentralAuthNotificationPlatform.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CentralAuthNotificationPlatform.Services;

public interface IUserLinkService
{
    Task<IReadOnlyList<UserLinkDto>> GetVisibleLinksAsync(Guid userId, CancellationToken cancellationToken);
    Task<CreateUserLinkResult> CreateAsync(Guid ownerUserId, CreateUserLinkRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteVisibleLinkAsync(Guid requesterUserId, Guid linkId, CancellationToken cancellationToken);
    Task<UserLinkStatusDto> GetForExternalAppAsync(Guid externalAppId, string externalUserId, CancellationToken cancellationToken);
    Task<CreateUserLinkResult> CreateForExternalAppAsync(
        Guid externalAppId,
        Guid platformUserId,
        string externalUserId,
        CancellationToken cancellationToken);
    Task<bool> DeleteForExternalAppAsync(Guid externalAppId, string externalUserId, CancellationToken cancellationToken);
}

public sealed record CreateUserLinkResult(UserLinkDto? Link, string? ErrorCode, string? ErrorMessage)
{
    public bool Succeeded => Link is not null;

    public static CreateUserLinkResult Success(UserLinkDto link) => new(link, null, null);

    public static CreateUserLinkResult Failure(string code, string message) => new(null, code, message);
}

public sealed class UserLinkService(
    AuthHubDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IAuditLogService auditLogService) : IUserLinkService
{
    public async Task<IReadOnlyList<UserLinkDto>> GetVisibleLinksAsync(Guid userId, CancellationToken cancellationToken)
    {
        var links = await dbContext.UserLinks
            .AsNoTracking()
            .Include(link => link.ExternalApp)
            .Include(link => link.PlatformUser)
            .Where(link =>
                link.PlatformUserId == userId ||
                (link.ExternalApp != null && link.ExternalApp.OwnerUserId == userId))
            .OrderBy(link => link.ExternalApp!.Name)
            .ThenBy(link => link.ExternalUserId)
            .ToListAsync(cancellationToken);

        return links.Select(ToDto).ToList();
    }

    public async Task<CreateUserLinkResult> CreateAsync(
        Guid ownerUserId,
        CreateUserLinkRequest request,
        CancellationToken cancellationToken)
    {
        var externalUserId = ResolveExternalUserId(request.ExternalUserId, request.ExternalEmail);
        if (string.IsNullOrWhiteSpace(externalUserId))
        {
            return CreateUserLinkResult.Failure("VALIDATION_ERROR", "External user id is required.");
        }

        var externalApp = await dbContext.ExternalApps
            .FirstOrDefaultAsync(app => app.Id == request.ExternalAppId && app.OwnerUserId == ownerUserId && app.IsActive, cancellationToken);

        if (externalApp is null)
        {
            return CreateUserLinkResult.Failure("APP_NOT_FOUND", "External app was not found for this account.");
        }

        var platformUser = string.IsNullOrWhiteSpace(request.PlatformEmail)
            ? await userManager.FindByIdAsync(ownerUserId.ToString())
            : await userManager.FindByEmailAsync(request.PlatformEmail.Trim());

        if (platformUser is null || !platformUser.IsActive)
        {
            return CreateUserLinkResult.Failure("PLATFORM_USER_NOT_FOUND", "Platform user was not found or is inactive.");
        }

        var normalizedExternalUserId = NormalizeExternalUserId(externalUserId);
        var duplicate = await dbContext.UserLinks.AnyAsync(
            link => link.ExternalAppId == externalApp.Id && link.NormalizedExternalUserId == normalizedExternalUserId,
            cancellationToken);

        if (duplicate)
        {
            return CreateUserLinkResult.Failure("LINK_EXISTS", "This external account is already linked for the selected app.");
        }

        var link = new UserLink
        {
            ExternalAppId = externalApp.Id,
            ExternalApp = externalApp,
            ExternalUserId = externalUserId.Trim(),
            NormalizedExternalUserId = normalizedExternalUserId,
            PlatformUserId = platformUser.Id,
            PlatformUser = platformUser
        };

        dbContext.UserLinks.Add(link);
        await dbContext.SaveChangesAsync(cancellationToken);
        await LogLinkAsync(AuditActions.AccountLinked, externalApp, platformUser, link.ExternalUserId, cancellationToken);
        return CreateUserLinkResult.Success(ToDto(link));
    }

    public async Task<bool> DeleteVisibleLinkAsync(
        Guid requesterUserId,
        Guid linkId,
        CancellationToken cancellationToken)
    {
        var link = await dbContext.UserLinks
            .Include(userLink => userLink.ExternalApp)
            .Include(userLink => userLink.PlatformUser)
            .FirstOrDefaultAsync(
                userLink =>
                    userLink.Id == linkId &&
                    (userLink.PlatformUserId == requesterUserId ||
                        (userLink.ExternalApp != null && userLink.ExternalApp.OwnerUserId == requesterUserId)),
                cancellationToken);

        if (link is null)
        {
            return false;
        }

        var externalApp = link.ExternalApp;
        var platformUser = link.PlatformUser;
        var externalUserId = link.ExternalUserId;

        dbContext.UserLinks.Remove(link);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(new AuditLog
        {
            Action = AuditActions.AccountUnlinked,
            UserId = platformUser?.Id,
            ExternalAppId = externalApp?.Id,
            AppName = externalApp?.Name,
            ExternalUserId = externalUserId,
            PlatformEmail = platformUser?.Email
        }, cancellationToken);

        return true;
    }

    public async Task<UserLinkStatusDto> GetForExternalAppAsync(
        Guid externalAppId,
        string externalUserId,
        CancellationToken cancellationToken)
    {
        var normalizedExternalUserId = NormalizeExternalUserId(externalUserId);
        var link = await dbContext.UserLinks
            .AsNoTracking()
            .Include(userLink => userLink.ExternalApp)
            .Include(userLink => userLink.PlatformUser)
            .FirstOrDefaultAsync(
                userLink =>
                    userLink.ExternalAppId == externalAppId &&
                    userLink.NormalizedExternalUserId == normalizedExternalUserId,
                cancellationToken);

        if (link is null)
        {
            return new UserLinkStatusDto(false, null, externalUserId.Trim(), null, null, null);
        }

        return new UserLinkStatusDto(
            true,
            link.ExternalApp?.Name,
            link.ExternalUserId,
            link.PlatformUser?.Email,
            link.PlatformUser?.DisplayName,
            link.CreatedAt);
    }

    public async Task<CreateUserLinkResult> CreateForExternalAppAsync(
        Guid externalAppId,
        Guid platformUserId,
        string externalUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalUserId))
        {
            return CreateUserLinkResult.Failure("VALIDATION_ERROR", "External user id is required.");
        }

        var externalApp = await dbContext.ExternalApps
            .FirstOrDefaultAsync(app => app.Id == externalAppId && app.IsActive, cancellationToken);

        if (externalApp is null)
        {
            return CreateUserLinkResult.Failure("APP_NOT_FOUND", "External app was not found.");
        }

        var platformUser = await userManager.FindByIdAsync(platformUserId.ToString());
        if (platformUser is null || !platformUser.IsActive)
        {
            return CreateUserLinkResult.Failure("PLATFORM_USER_NOT_FOUND", "Platform user was not found or is inactive.");
        }

        var normalizedExternalUserId = NormalizeExternalUserId(externalUserId);
        var existing = await dbContext.UserLinks
            .Include(link => link.ExternalApp)
            .Include(link => link.PlatformUser)
            .FirstOrDefaultAsync(
                link => link.ExternalAppId == externalApp.Id && link.NormalizedExternalUserId == normalizedExternalUserId,
                cancellationToken);

        if (existing is not null)
        {
            return existing.PlatformUserId == platformUser.Id
                ? CreateUserLinkResult.Success(ToDto(existing))
                : CreateUserLinkResult.Failure("LINK_EXISTS", "This external account is already linked to another platform user.");
        }

        var link = new UserLink
        {
            ExternalAppId = externalApp.Id,
            ExternalApp = externalApp,
            ExternalUserId = externalUserId.Trim(),
            NormalizedExternalUserId = normalizedExternalUserId,
            PlatformUserId = platformUser.Id,
            PlatformUser = platformUser
        };

        dbContext.UserLinks.Add(link);
        await dbContext.SaveChangesAsync(cancellationToken);
        await LogLinkAsync(AuditActions.AccountLinked, externalApp, platformUser, link.ExternalUserId, cancellationToken);
        return CreateUserLinkResult.Success(ToDto(link));
    }

    public async Task<bool> DeleteForExternalAppAsync(
        Guid externalAppId,
        string externalUserId,
        CancellationToken cancellationToken)
    {
        var normalizedExternalUserId = NormalizeExternalUserId(externalUserId);
        var link = await dbContext.UserLinks
            .Include(userLink => userLink.ExternalApp)
            .Include(userLink => userLink.PlatformUser)
            .FirstOrDefaultAsync(
                userLink =>
                    userLink.ExternalAppId == externalAppId &&
                    userLink.NormalizedExternalUserId == normalizedExternalUserId,
                cancellationToken);

        if (link is null)
        {
            return false;
        }

        var externalApp = link.ExternalApp;
        var platformUser = link.PlatformUser;
        var linkExternalUserId = link.ExternalUserId;

        dbContext.UserLinks.Remove(link);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(new AuditLog
        {
            Action = AuditActions.AccountUnlinked,
            UserId = platformUser?.Id,
            ExternalAppId = externalApp?.Id,
            AppName = externalApp?.Name,
            ExternalUserId = linkExternalUserId,
            PlatformEmail = platformUser?.Email
        }, cancellationToken);

        return true;
    }

    private static UserLinkDto ToDto(UserLink link)
    {
        return new UserLinkDto(
            link.Id,
            link.ExternalAppId,
            link.ExternalApp?.Name ?? string.Empty,
            link.ExternalUserId,
            link.PlatformUserId,
            link.PlatformUser?.Email ?? string.Empty,
            link.PlatformUser?.DisplayName ?? string.Empty,
            link.CreatedAt);
    }

    private Task LogLinkAsync(
        string action,
        ExternalApp externalApp,
        ApplicationUser platformUser,
        string externalUserId,
        CancellationToken cancellationToken)
    {
        return auditLogService.LogAsync(new AuditLog
        {
            Action = action,
            UserId = platformUser.Id,
            ExternalAppId = externalApp.Id,
            AppName = externalApp.Name,
            ExternalUserId = externalUserId,
            PlatformEmail = platformUser.Email
        }, cancellationToken);
    }

    private static string ResolveExternalUserId(string? externalUserId, string? externalEmail)
    {
        return string.IsNullOrWhiteSpace(externalUserId)
            ? externalEmail?.Trim() ?? string.Empty
            : externalUserId.Trim();
    }

    private static string NormalizeExternalUserId(string externalUserId)
    {
        return externalUserId.Trim().ToUpperInvariant();
    }
}
