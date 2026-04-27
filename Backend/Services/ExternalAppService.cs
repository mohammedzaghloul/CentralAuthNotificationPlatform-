using CentralAuthNotificationPlatform.Dtos;
using CentralAuthNotificationPlatform.Models;
using CentralAuthNotificationPlatform.Repositories;
using CentralAuthNotificationPlatform.Security;

namespace CentralAuthNotificationPlatform.Services;

public interface IExternalAppService
{
    Task<IReadOnlyList<ExternalAppDto>> GetOwnedAppsAsync(Guid ownerUserId, CancellationToken cancellationToken);
    Task<CreateExternalAppResult> CreateAsync(Guid ownerUserId, CreateExternalAppRequest request, CancellationToken cancellationToken);
    Task<RegeneratedApiKeyDto?> RegenerateApiKeyAsync(Guid ownerUserId, Guid appId, CancellationToken cancellationToken);
    Task<bool> RevokeApiKeyAsync(Guid ownerUserId, Guid appId, CancellationToken cancellationToken);
    Task<ExternalApp?> ValidateApiKeyAsync(string? apiKey, CancellationToken cancellationToken);
}

public sealed record CreateExternalAppResult(CreatedExternalAppDto? App, string? ErrorCode, string? ErrorMessage, int StatusCode)
{
    public bool Succeeded => App is not null;

    public static CreateExternalAppResult Success(CreatedExternalAppDto app) =>
        new(app, null, null, StatusCodes.Status201Created);

    public static CreateExternalAppResult Failure(string code, string message, int statusCode) =>
        new(null, code, message, statusCode);
}

public sealed class ExternalAppService(IExternalAppRepository externalAppRepository) : IExternalAppService
{
    public async Task<IReadOnlyList<ExternalAppDto>> GetOwnedAppsAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        var apps = await externalAppRepository.ListOwnedAsync(ownerUserId, cancellationToken);
        return apps.Select(ToDto).ToList();
    }

    public async Task<CreateExternalAppResult> CreateAsync(
        Guid ownerUserId,
        CreateExternalAppRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.Trim();
        var exists = await externalAppRepository.ExistsForOwnerAsync(ownerUserId, normalizedName, cancellationToken);
        if (exists)
        {
            return CreateExternalAppResult.Failure(
                "APP_NAME_EXISTS",
                "You already have an app with this name.",
                StatusCodes.Status409Conflict);
        }

        if (!Uri.TryCreate(request.RedirectUri.Trim(), UriKind.Absolute, out var redirectUri))
        {
            return CreateExternalAppResult.Failure(
                "INVALID_REDIRECT_URI",
                "Redirect URI must be an absolute URL.",
                StatusCodes.Status400BadRequest);
        }

        var domain = NormalizeDomain(request.Domain, redirectUri);
        if (domain is null)
        {
            return CreateExternalAppResult.Failure(
                "INVALID_DOMAIN",
                "Domain must be a valid host name.",
                StatusCodes.Status400BadRequest);
        }

        if (!HostMatchesDomain(redirectUri.Host, domain))
        {
            return CreateExternalAppResult.Failure(
                "REDIRECT_DOMAIN_MISMATCH",
                "Redirect URI host must match the registered app domain.",
                StatusCodes.Status400BadRequest);
        }

        var clientId = TokenUtility.GenerateClientId();
        var clientSecret = TokenUtility.GenerateClientSecret();
        var apiKey = TokenUtility.GenerateApiKey();
        var app = new ExternalApp
        {
            OwnerUserId = ownerUserId,
            Name = normalizedName,
            Domain = domain,
            ClientId = clientId,
            ClientSecretHash = TokenUtility.Sha256(clientSecret),
            RedirectUri = redirectUri.ToString(),
            ApiKeyHash = TokenUtility.Sha256(apiKey),
            ApiKeyPreview = Preview(apiKey),
            IsApiKeyActive = true
        };

        await externalAppRepository.AddAsync(app, cancellationToken);

        return CreateExternalAppResult.Success(new CreatedExternalAppDto(
            app.Id,
            app.Name,
            app.Domain,
            app.ClientId,
            clientSecret,
            app.RedirectUri,
            apiKey,
            app.ApiKeyPreview,
            app.CreatedAt));
    }

    public async Task<RegeneratedApiKeyDto?> RegenerateApiKeyAsync(
        Guid ownerUserId,
        Guid appId,
        CancellationToken cancellationToken)
    {
        var app = await externalAppRepository.FindByIdAsync(appId, cancellationToken);
        if (app is null || app.OwnerUserId != ownerUserId)
        {
            return null;
        }

        var apiKey = TokenUtility.GenerateApiKey();
        app.ApiKeyHash = TokenUtility.Sha256(apiKey);
        app.ApiKeyPreview = Preview(apiKey);
        app.IsApiKeyActive = true;
        await externalAppRepository.SaveChangesAsync(cancellationToken);

        return new RegeneratedApiKeyDto(app.Id, apiKey, app.ApiKeyPreview, app.IsApiKeyActive, DateTimeOffset.UtcNow);
    }

    public async Task<bool> RevokeApiKeyAsync(Guid ownerUserId, Guid appId, CancellationToken cancellationToken)
    {
        var app = await externalAppRepository.FindByIdAsync(appId, cancellationToken);
        if (app is null || app.OwnerUserId != ownerUserId)
        {
            return false;
        }

        app.IsApiKeyActive = false;
        await externalAppRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ExternalApp?> ValidateApiKeyAsync(string? apiKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var apiKeyHash = TokenUtility.Sha256(apiKey.Trim());
        var app = await externalAppRepository.FindByApiKeyHashAsync(apiKeyHash, cancellationToken);
        if (app is null || !app.IsApiKeyActive)
        {
            return null;
        }

        app.LastUsedAt = DateTimeOffset.UtcNow;
        await externalAppRepository.SaveChangesAsync(cancellationToken);
        return app;
    }

    private static ExternalAppDto ToDto(ExternalApp app)
    {
        return new ExternalAppDto(
            app.Id,
            app.Name,
            app.Domain,
            app.ClientId,
            app.RedirectUri,
            app.ApiKeyPreview,
            app.IsApiKeyActive,
            app.IsActive,
            app.CreatedAt,
            app.LastUsedAt);
    }

    private static string Preview(string secret)
    {
        return $"{secret[..8]}...{secret[^4..]}";
    }

    private static string? NormalizeDomain(string? domain, Uri redirectUri)
    {
        var candidate = string.IsNullOrWhiteSpace(domain) ? redirectUri.Host : domain.Trim();
        candidate = candidate.Trim().TrimEnd('/').ToLowerInvariant();
        if (candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Uri.CheckHostName(candidate) == UriHostNameType.Unknown ? null : candidate;
    }

    private static bool HostMatchesDomain(string host, string domain)
    {
        return string.Equals(host, domain, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase);
    }
}
