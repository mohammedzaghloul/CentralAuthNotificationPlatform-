using CentralAuthNotificationPlatform.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Net;
using System.Text;

namespace CentralAuthNotificationPlatform.Controllers;

[ApiController]
[Route("connect")]
public sealed class ConnectController(
    IOAuthService oauthService,
    IWebHostEnvironment environment) : ControllerBase
{
    private const string IdentityApplicationScheme = "Identity.Application";

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

        return Content(RenderConsentPage(request), "text/html", Encoding.UTF8);
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
            return $"http://localhost:4300/dashboard/?returnUrl={Uri.EscapeDataString(returnUrl)}";
        }

        return $"/dashboard.html?returnUrl={Uri.EscapeDataString(returnUrl)}";
    }

    private static string RenderConsentPage(Dtos.OAuthAuthorizationRequestDetails request)
    {
        static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

        var scopes = request.Scope
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(scope => $"<span class=\"scope-badge\">{E(scope)}</span>");

        return $$"""
<!doctype html>
<html lang="ar" dir="rtl">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>السماح بالربط - بوابة الدخول الموحد</title>
  <link href="https://fonts.googleapis.com/css2?family=IBM+Plex+Sans+Arabic:wght@300;400;500;600;700&display=swap" rel="stylesheet">
  <style>
    :root {
      --primary: #4f46e5;
      --primary-hover: #4338ca;
      --primary-light: rgba(79, 70, 229, 0.08);
      --background: #f1f5f9;
      --surface: #ffffff;
      --text-main: #0f172a;
      --text-secondary: #334155;
      --text-muted: #64748b;
      --text-light: #94a3b8;
      --border: #e2e8f0;
      --border-strong: #cbd5e1;
      --radius-md: 8px;
      --radius-lg: 10px;
      --radius-xl: 14px;
      --shadow-sm: 0 1px 2px 0 rgb(0 0 0 / 0.04);
      --shadow-md: 0 4px 6px -1px rgb(0 0 0 / 0.05), 0 2px 4px -2px rgb(0 0 0 / 0.03);
      --shadow-lg: 0 10px 15px -3px rgb(0 0 0 / 0.06), 0 4px 6px -4px rgb(0 0 0 / 0.03);
    }
    * { margin: 0; padding: 0; box-sizing: border-box; }
    body {
      font-family: 'IBM Plex Sans Arabic', -apple-system, BlinkMacSystemFont, sans-serif;
      background: var(--background);
      color: var(--text-main);
      line-height: 1.5;
      min-height: 100vh;
    }
    .consent-shell {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 2rem 1rem;
    }
    .consent-card {
      max-width: 480px;
      width: 100%;
      background: var(--surface);
      border: 1px solid var(--border);
      border-radius: var(--radius-xl);
      box-shadow: var(--shadow-md);
      padding: 2rem;
    }
    .brand-header {
      text-align: center;
      margin-bottom: 2rem;
    }
    .brand-icon {
      width: 44px;
      height: 44px;
      background: var(--primary);
      color: white;
      border-radius: 10px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      font-weight: 800;
      font-size: 0.875rem;
      margin-bottom: 1rem;
    }
    .request-title {
      font-size: 0.75rem;
      color: var(--text-muted);
      font-weight: 500;
      margin-bottom: 0.5rem;
    }
    .app-name {
      font-size: 1.25rem;
      font-weight: 700;
      color: var(--text-main);
      margin-bottom: 0.5rem;
    }
    .description {
      font-size: 0.875rem;
      color: var(--text-secondary);
      margin-bottom: 1.5rem;
    }
    .scopes-section {
      background: var(--primary-light);
      border: 1px solid var(--border);
      border-radius: var(--radius-lg);
      padding: 1rem;
      margin-bottom: 1.5rem;
    }
    .scopes-title {
      font-size: 0.75rem;
      font-weight: 600;
      color: var(--text-secondary);
      text-transform: uppercase;
      letter-spacing: 0.02em;
      margin-bottom: 0.75rem;
    }
    .scopes-list {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
    }
    .scope-badge {
      display: inline-flex;
      align-items: center;
      padding: 0.375rem 0.75rem;
      border-radius: var(--radius-md);
      background: var(--surface);
      border: 1px solid var(--border);
      font-size: 0.8125rem;
      font-weight: 500;
      color: var(--text-secondary);
    }
    .actions {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }
    .btn {
      font-family: inherit;
      font-weight: 500;
      font-size: 0.9rem;
      padding: 0.75rem 1.5rem;
      border-radius: var(--radius-md);
      border: none;
      cursor: pointer;
      transition: all 0.15s ease;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      text-decoration: none;
    }
    .btn:active { transform: scale(0.98); }
    .btn-primary {
      background: var(--primary);
      color: white;
      box-shadow: var(--shadow-sm);
    }
    .btn-primary:hover {
      background: var(--primary-hover);
      box-shadow: var(--shadow-md);
    }
    .btn-secondary {
      background: var(--surface);
      color: var(--text-secondary);
      border: 1px solid var(--border);
    }
    .btn-secondary:hover {
      background: var(--background);
      border-color: var(--border-strong);
    }
    .footer-note {
      text-align: center;
      margin-top: 1.5rem;
      font-size: 0.75rem;
      color: var(--text-light);
    }
    @media (max-width: 480px) {
      .consent-card { padding: 1.5rem; }
      .app-name { font-size: 1.1rem; }
    }
  </style>
</head>
<body>
  <main class="consent-shell">
    <section class="consent-card">
      <div class="brand-header">
        <div class="brand-icon">IDP</div>
        <p class="request-title">طلب ربط حساب</p>
        <h1 class="app-name">{{E(request.AppName)}} يريد ربط حسابك</h1>
      </div>
      
      <p class="description">
        سيحصل التطبيق على هويتك داخل منصة الدخول الموحد حتى يتم ربط الحسابين بشكل آمن.
      </p>
      
      <div class="scopes-section">
        <p class="scopes-title">الصلاحيات المطلوبة</p>
        <div class="scopes-list">
          {{string.Join("", scopes)}}
        </div>
      </div>
      
      <form method="post" action="/connect/authorize" class="actions">
        <input type="hidden" name="response_type" value="{{E(request.ResponseType)}}">
        <input type="hidden" name="client_id" value="{{E(request.ClientId)}}">
        <input type="hidden" name="redirect_uri" value="{{E(request.RedirectUri)}}">
        <input type="hidden" name="scope" value="{{E(request.Scope)}}">
        <input type="hidden" name="state" value="{{E(request.State)}}">
        <input type="hidden" name="nonce" value="{{E(request.Nonce)}}">
        <input type="hidden" name="code_challenge" value="{{E(request.CodeChallenge)}}">
        <input type="hidden" name="code_challenge_method" value="{{E(request.CodeChallengeMethod)}}">
        <button class="btn btn-primary" type="submit" name="consent" value="allow">السماح بالربط</button>
        <button class="btn btn-secondary" type="submit" name="consent" value="deny">إلغاء</button>
      </form>
      
      <p class="footer-note">
        بوابة الدخول الموحد - منصة مركزية آمنة
      </p>
    </section>
  </main>
</body>
</html>
""";
    }
}
