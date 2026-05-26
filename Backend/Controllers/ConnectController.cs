using CentralAuthNotificationPlatform.BLL.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CentralAuthNotificationPlatform.PL.Controllers;

[Route("connect")]
public sealed class ConnectController(
    IOAuthService oauthService,
    IWebHostEnvironment environment,
    IConfiguration configuration) : Controller
{
    private const string IdentityApplicationScheme = "Identity.Application";
    private const string DefaultProductionDashboardUrl = "https://resplendent-cooperation-production-eeb8.up.railway.app/dashboard/";

    [HttpGet("authorize")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Authorize(
        [FromQuery(Name = "response_type")] string responseType,
        [FromQuery(Name = "client_id")] string clientId,
        [FromQuery(Name = "redirect_uri")] string redirectUri,
        [FromQuery] string? scope,
        [FromQuery] string? state,
        [FromQuery] string? nonce,
        [FromQuery(Name = "code_challenge")] string? codeChallenge,
        [FromQuery(Name = "code_challenge_method")] string? codeChallengeMethod,
        CancellationToken cancellationToken)
    {
        var authResult = await HttpContext.AuthenticateAsync(IdentityApplicationScheme);
        if (!authResult.Succeeded || authResult.Principal?.Identity?.IsAuthenticated != true)
        {
            return Redirect(BuildLoginRedirectUrl());
        }

        HttpContext.User = authResult.Principal;

        var request = await oauthService.ValidateAuthorizationRequestAsync(
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
            return BadRequest(new { error = "invalid_request", error_description = "Authorization request is invalid." });
        }

        return View("Consent", request);
    }

    [HttpPost("authorize")]
    [Authorize(AuthenticationSchemes = IdentityApplicationScheme)]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> AuthorizePost(CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType)
        {
            return BadRequest(new { error = "invalid_request", error_description = "Authorization request must use application/x-www-form-urlencoded." });
        }

        var form = await Request.ReadFormAsync(cancellationToken);
        var consent = form["consent"].FirstOrDefault();
        var responseType = form["response_type"].FirstOrDefault() ?? string.Empty;
        var clientId = form["client_id"].FirstOrDefault() ?? string.Empty;
        var redirectUri = form["redirect_uri"].FirstOrDefault() ?? string.Empty;
        var scope = form["scope"].FirstOrDefault();
        var state = form["state"].FirstOrDefault();
        var nonce = form["nonce"].FirstOrDefault();
        var codeChallenge = form["code_challenge"].FirstOrDefault();
        var codeChallengeMethod = form["code_challenge_method"].FirstOrDefault();

        if (!string.Equals(consent, "allow", StringComparison.Ordinal))
        {
            var deniedRedirect = await oauthService.CreateAccessDeniedRedirectAsync(clientId, redirectUri, state, cancellationToken);
            return deniedRedirect is null
                ? BadRequest(new { error = "invalid_request", error_description = "Authorization request is invalid." })
                : Redirect(deniedRedirect.ToString());
        }

        var redirect = await oauthService.CreateAuthorizationRedirectAsync(
            User,
            responseType,
            clientId,
            redirectUri,
            scope,
            state,
            nonce,
            codeChallenge,
            codeChallengeMethod,
            cancellationToken);

        if (redirect is null)
        {
            return BadRequest(new { error = "invalid_request", error_description = "Authorization request is invalid." });
        }

        return Redirect(redirect.ToString());
    }

    [HttpPost("token")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Token(CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType)
        {
            return BadRequest(new { error = "invalid_request", error_description = "Token request must use application/x-www-form-urlencoded." });
        }

        var token = await oauthService.ExchangeCodeAsync(await Request.ReadFormAsync(cancellationToken), cancellationToken);
        if (token is null)
        {
            return BadRequest(new { error = "invalid_grant", error_description = "Authorization code or client credentials are invalid." });
        }

        return Ok(new
        {
            access_token = token.AccessToken,
            id_token = token.IdToken,
            token_type = token.TokenType,
            expires_in = token.ExpiresIn,
            scope = token.Scope
        });
    }

    [HttpGet("userinfo")]
    [Authorize]
    public async Task<IActionResult> UserInfo(CancellationToken cancellationToken)
    {
        var userInfo = await oauthService.GetUserInfoAsync(User, cancellationToken);
        return userInfo is null ? Challenge() : Ok(userInfo);
    }

    private string BuildLoginRedirectUrl()
    {
        var returnUrl = UriHelper.BuildAbsolute(
            Request.Scheme,
            Request.Host,
            Request.PathBase,
            Request.Path,
            Request.QueryString);

        if (environment.IsDevelopment())
        {
            return $"http://localhost:4300/login?returnUrl={Uri.EscapeDataString(returnUrl)}";
        }

        var dashboardUrl = ResolveDashboardUrl();
        var loginUrl = dashboardUrl.Replace("/dashboard/", "/login", StringComparison.OrdinalIgnoreCase);
        return $"{loginUrl}?returnUrl={Uri.EscapeDataString(returnUrl)}";
    }

    private string ResolveDashboardUrl()
    {
        var configuredUrls = new[]
        {
            configuration["AUTH_HUB_DASHBOARD_URL"],
            configuration["DASHBOARD_URL"],
            configuration["CENTRAL_AUTH_PORTAL_URL"],
            configuration["CENTRAL_AUTH_FRONTEND_URL"],
            configuration["RAILWAY_SERVICE_RESPLENDENT_COOPERATION_URL"],
            configuration["FRONTEND_URL"],
            configuration["CLIENT_APP_URL"]
        };

        foreach (var configuredUrl in configuredUrls)
        {
            var dashboardUrl = NormalizeDashboardUrl(configuredUrl);
            if (!string.IsNullOrWhiteSpace(dashboardUrl))
            {
                return dashboardUrl;
            }
        }

        var dashboardOrigin = configuration["ALLOWED_ORIGINS"]
            ?.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(origin =>
                origin.Contains("resplendent", StringComparison.OrdinalIgnoreCase)
                || origin.Contains("dashboard", StringComparison.OrdinalIgnoreCase));

        return NormalizeDashboardUrl(dashboardOrigin) ?? DefaultProductionDashboardUrl;
    }

    private static string? NormalizeDashboardUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        var builder = new UriBuilder(uri)
        {
            Query = string.Empty,
            Fragment = string.Empty
        };

        var path = builder.Path.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            builder.Path = "/dashboard/";
        }
        else if (path.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            builder.Path = path;
        }
        else if (path.EndsWith("/dashboard", StringComparison.OrdinalIgnoreCase))
        {
            builder.Path = $"{path}/";
        }
        else
        {
            builder.Path = $"{path}/dashboard/";
        }

        return builder.Uri.ToString().TrimEnd('?');
    }

}
