using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CentralAuthNotificationPlatform.Dtos;
using CentralAuthNotificationPlatform.Models;
using CentralAuthNotificationPlatform.Options;
using CentralAuthNotificationPlatform.Repositories;
using CentralAuthNotificationPlatform.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CentralAuthNotificationPlatform.Services;

public interface IOAuthService
{
    Task<OAuthAuthorizationRequestDetails?> ValidateAuthorizationRequestAsync(
        string responseType,
        string clientId,
        string redirectUri,
        string? scope,
        string? state,
        string? nonce,
        string? codeChallenge,
        string? codeChallengeMethod,
        CancellationToken cancellationToken);

    Task<Uri?> CreateAuthorizationRedirectAsync(
        ClaimsPrincipal principal,
        string responseType,
        string clientId,
        string redirectUri,
        string? scope,
        string? state,
        string? nonce,
        string? codeChallenge,
        string? codeChallengeMethod,
        CancellationToken cancellationToken);

    Task<OAuthTokenResponse?> ExchangeCodeAsync(IFormCollection form, CancellationToken cancellationToken);
    Task<Uri?> CreateAccessDeniedRedirectAsync(string clientId, string redirectUri, string? state, CancellationToken cancellationToken);
    Task<object?> GetUserInfoAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}

public sealed class OAuthService(
    UserManager<ApplicationUser> userManager,
    IExternalAppRepository externalAppRepository,
    IOAuthRepository oauthRepository,
    IJwtTokenService jwtTokenService,
    IOptions<JwtOptions> jwtOptions) : IOAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<OAuthAuthorizationRequestDetails?> ValidateAuthorizationRequestAsync(
        string responseType,
        string clientId,
        string redirectUri,
        string? scope,
        string? state,
        string? nonce,
        string? codeChallenge,
        string? codeChallengeMethod,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(responseType, "code", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(redirectUri))
        {
            return null;
        }

        var app = await externalAppRepository.FindByClientIdAsync(clientId, cancellationToken);
        if (app is null || !UriMatches(app.RedirectUri, redirectUri))
        {
            return null;
        }

        return new OAuthAuthorizationRequestDetails(
            app.Name,
            app.ClientId,
            redirectUri.Trim(),
            "code",
            NormalizeScope(scope),
            state,
            nonce,
            string.IsNullOrWhiteSpace(codeChallenge) ? null : codeChallenge.Trim(),
            string.IsNullOrWhiteSpace(codeChallengeMethod) ? null : codeChallengeMethod.Trim());
    }

    public async Task<Uri?> CreateAuthorizationRedirectAsync(
        ClaimsPrincipal principal,
        string responseType,
        string clientId,
        string redirectUri,
        string? scope,
        string? state,
        string? nonce,
        string? codeChallenge,
        string? codeChallengeMethod,
        CancellationToken cancellationToken)
    {
        var request = await ValidateAuthorizationRequestAsync(
            responseType,
            clientId,
            redirectUri,
            scope,
            state,
            nonce,
            codeChallenge,
            codeChallengeMethod,
            cancellationToken);

        if (request is null)
        {
            return null;
        }

        var app = await externalAppRepository.FindByClientIdAsync(clientId, cancellationToken);
        if (app is null)
        {
            return null;
        }

        var user = await userManager.GetUserAsync(principal);
        if (user is null || !user.IsActive)
        {
            return null;
        }

        var rawCode = TokenUtility.GenerateAuthorizationCode();
        var code = new OAuthAuthorizationCode
        {
            UserId = user.Id,
            ExternalAppId = app.Id,
            CodeHash = TokenUtility.Sha256(rawCode),
            RedirectUri = request.RedirectUri,
            Scope = request.Scope,
            Nonce = request.Nonce,
            CodeChallenge = request.CodeChallenge,
            CodeChallengeMethod = request.CodeChallengeMethod,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(_jwtOptions.AuthorizationCodeMinutes, 5, 10))
        };

        await oauthRepository.EnsureUserAppAsync(user.Id, app.Id, cancellationToken);
        await oauthRepository.AddAuthorizationCodeAsync(code, cancellationToken);

        var builder = new UriBuilder(request.RedirectUri);
        var query = new List<string>
        {
            $"code={Uri.EscapeDataString(rawCode)}"
        };

        if (!string.IsNullOrWhiteSpace(request.State))
        {
            query.Add($"state={Uri.EscapeDataString(request.State)}");
        }

        builder.Query = string.Join("&", query);
        return builder.Uri;
    }

    public async Task<Uri?> CreateAccessDeniedRedirectAsync(
        string clientId,
        string redirectUri,
        string? state,
        CancellationToken cancellationToken)
    {
        var app = await externalAppRepository.FindByClientIdAsync(clientId, cancellationToken);
        if (app is null || !UriMatches(app.RedirectUri, redirectUri))
        {
            return null;
        }

        var builder = new UriBuilder(redirectUri);
        var query = new List<string>
        {
            "error=access_denied"
        };

        if (!string.IsNullOrWhiteSpace(state))
        {
            query.Add($"state={Uri.EscapeDataString(state)}");
        }

        builder.Query = string.Join("&", query);
        return builder.Uri;
    }

    public async Task<OAuthTokenResponse?> ExchangeCodeAsync(IFormCollection form, CancellationToken cancellationToken)
    {
        var grantType = form["grant_type"].FirstOrDefault();
        var rawCode = form["code"].FirstOrDefault();
        var redirectUri = form["redirect_uri"].FirstOrDefault();
        var clientId = form["client_id"].FirstOrDefault();
        var clientSecret = form["client_secret"].FirstOrDefault();
        var codeVerifier = form["code_verifier"].FirstOrDefault();

        if (!string.Equals(grantType, "authorization_code", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(rawCode) ||
            string.IsNullOrWhiteSpace(redirectUri) ||
            string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(clientSecret))
        {
            return null;
        }

        var app = await externalAppRepository.FindByClientIdAsync(clientId, cancellationToken);
        if (app is null ||
            !UriMatches(app.RedirectUri, redirectUri) ||
            !TokenUtility.FixedTimeEquals(TokenUtility.Sha256(clientSecret), app.ClientSecretHash))
        {
            return null;
        }

        var code = await oauthRepository.FindActiveAuthorizationCodeAsync(TokenUtility.Sha256(rawCode), cancellationToken);
        if (code?.User is null ||
            code.ExternalAppId != app.Id ||
            !UriMatches(code.RedirectUri, redirectUri) ||
            !ValidatePkce(code, codeVerifier))
        {
            return null;
        }

        code.IsUsed = true;
        code.UsedAt = DateTimeOffset.UtcNow;
        await oauthRepository.SaveChangesAsync(cancellationToken);

        var expires = Math.Clamp(_jwtOptions.OAuthAccessTokenMinutes, 5, 10);
        var accessToken = jwtTokenService.CreateAccessToken(code.User, _jwtOptions.Audience, code.Scope, expires);
        var idToken = jwtTokenService.CreateIdToken(code.User, app.ClientId, code.Nonce, expires);

        return new OAuthTokenResponse(
            accessToken,
            idToken,
            "Bearer",
            expires * 60,
            code.Scope);
    }

    public async Task<object?> GetUserInfoAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return null;
        }

        return new
        {
            sub = user.Id.ToString(),
            email = user.Email,
            name = user.DisplayName
        };
    }

    private static string NormalizeScope(string? scope)
    {
        var scopes = (scope ?? "openid profile email")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (!scopes.Contains("openid", StringComparer.Ordinal))
        {
            scopes.Insert(0, "openid");
        }

        return string.Join(' ', scopes);
    }

    private static bool UriMatches(string expected, string actual)
    {
        return Uri.TryCreate(expected, UriKind.Absolute, out var expectedUri) &&
            Uri.TryCreate(actual, UriKind.Absolute, out var actualUri) &&
            Uri.Compare(expectedUri, actualUri, UriComponents.AbsoluteUri, UriFormat.SafeUnescaped, StringComparison.Ordinal) == 0;
    }

    private static bool ValidatePkce(OAuthAuthorizationCode code, string? verifier)
    {
        if (string.IsNullOrWhiteSpace(code.CodeChallenge))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(verifier))
        {
            return false;
        }

        if (string.Equals(code.CodeChallengeMethod, "S256", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
            return string.Equals(TokenUtility.ToBase64Url(bytes), code.CodeChallenge, StringComparison.Ordinal);
        }

        return string.Equals(code.CodeChallenge, verifier, StringComparison.Ordinal);
    }
}
