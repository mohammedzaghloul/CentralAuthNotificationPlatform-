using System.ComponentModel.DataAnnotations;

namespace CentralAuthNotificationPlatform.Dtos;

public sealed record CreateExternalAppRequest(
    [Required, MinLength(2), MaxLength(120)] string Name,
    [MaxLength(253)] string? Domain,
    [Required, Url, MaxLength(500)] string RedirectUri);

public sealed record ExternalAppDto(
    Guid Id,
    string Name,
    string Domain,
    string ClientId,
    string RedirectUri,
    string ApiKeyPreview,
    bool IsApiKeyActive,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt);

public sealed record CreatedExternalAppDto(
    Guid Id,
    string Name,
    string Domain,
    string ClientId,
    string ClientSecret,
    string RedirectUri,
    string ApiKey,
    string ApiKeyPreview,
    DateTimeOffset CreatedAt);

public sealed record RegeneratedApiKeyDto(
    Guid Id,
    string ApiKey,
    string ApiKeyPreview,
    bool IsApiKeyActive,
    DateTimeOffset RegeneratedAt);

public sealed record OAuthTokenResponse(
    string AccessToken,
    string IdToken,
    string TokenType,
    int ExpiresIn,
    string Scope);

public sealed record OAuthAuthorizationRequestDetails(
    string AppName,
    string ClientId,
    string RedirectUri,
    string ResponseType,
    string Scope,
    string? State,
    string? Nonce,
    string? CodeChallenge,
    string? CodeChallengeMethod);

public sealed record ValidateIntegrationResetTokenRequest(
    [Required, MinLength(20), MaxLength(512)] string Token);

public sealed record ValidateIntegrationResetTokenResponse(
    bool Valid,
    string? ExternalUserId);

public sealed record MarkIntegrationResetTokenUsedRequest(
    [Required, MinLength(20), MaxLength(512)] string Token);
