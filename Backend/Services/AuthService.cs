using System.Text.Json;
using CentralAuthNotificationPlatform.Data;
using CentralAuthNotificationPlatform.Dtos;
using CentralAuthNotificationPlatform.Models;
using CentralAuthNotificationPlatform.Options;
using CentralAuthNotificationPlatform.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CentralAuthNotificationPlatform.Services;

public interface IAuthService
{
    Task<AuthResponse?> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthResponse?> GetSessionAsync(Guid userId, CancellationToken cancellationToken);
    Task<ForgotPasswordStartResult> StartForgotPasswordAsync(
        ForgotPasswordRequest request,
        ExternalApp? externalApp,
        CancellationToken cancellationToken);
    Task<ValidateResetTokenResponse> ValidateResetTokenAsync(ValidateResetTokenRequest request, CancellationToken cancellationToken);
    Task<bool> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);
    Task<ValidateIntegrationResetTokenResponse> ValidateExternalResetTokenAsync(
        string rawToken,
        ExternalApp externalApp,
        CancellationToken cancellationToken);
    Task<bool> MarkExternalResetTokenUsedAsync(
        string rawToken,
        ExternalApp externalApp,
        CancellationToken cancellationToken);
}

public sealed class AuthService(
    AuthHubDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IJwtTokenService jwtTokenService,
    INotificationService notificationService,
    IAppEmailSender emailSender,
    IAuditLogService auditLogService,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    private const int MaxTokenAttempts = 5;
    private static readonly TimeSpan ResetRequestCooldown = TimeSpan.FromSeconds(60);
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            return null;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = request.DisplayName.Trim()
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return null;
        }

        var role = PlatformRoles.Normalize(request.Role);
        await userManager.AddToRoleAsync(user, role);
        await signInManager.SignInAsync(user, isPersistent: false);
        return jwtTokenService.CreatePlatformToken(user, [role]);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive)
        {
            return null;
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        await signInManager.SignInAsync(user, isPersistent: false);
        return jwtTokenService.CreatePlatformToken(user, roles.Count == 0 ? [PlatformRoles.User] : roles.ToArray());
    }

    public async Task<AuthResponse?> GetSessionAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        return jwtTokenService.CreatePlatformToken(user, roles.Count == 0 ? [PlatformRoles.User] : roles.ToArray());
    }

    public async Task<ForgotPasswordStartResult> StartForgotPasswordAsync(
        ForgotPasswordRequest request,
        ExternalApp? externalApp,
        CancellationToken cancellationToken)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(_jwtOptions.PasswordResetMinutes, 5, 10));
        var externalUserId = request.UserIdentifier;
        var user = await ResolvePasswordResetUserAsync(externalUserId, externalApp, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return ForgotPasswordStartResult.NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        var externalAppId = externalApp?.Id;
        var recentRequest = await dbContext.PasswordResetTokens
            .AsNoTracking()
            .Where(token =>
                token.UserId == user.Id &&
                token.ExternalAppId == externalAppId &&
                !token.IsUsed &&
                token.ExpiresAt > now &&
                token.CreatedAt > now.Subtract(ResetRequestCooldown))
            .OrderByDescending(token => token.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (recentRequest is not null)
        {
            await auditLogService.LogAsync(new AuditLog
            {
                Action = AuditActions.ForgotPasswordRateLimited,
                UserId = user.Id,
                ExternalAppId = externalApp?.Id,
                AppName = externalApp?.Name,
                ExternalUserId = externalUserId,
                PlatformEmail = user.Email
            }, cancellationToken);

            return ForgotPasswordStartResult.RateLimited(new ForgotPasswordResponse(
                false,
                "Please wait 60 seconds before requesting another password reset.",
                recentRequest.ExpiresAt));
        }

        var activeTokens = await dbContext.PasswordResetTokens
            .Where(token =>
                token.UserId == user.Id &&
                token.ExternalAppId == externalAppId &&
                !token.IsUsed &&
                token.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.IsUsed = true;
            token.UsedAt = now;
        }

        var rawTokenSecret = TokenUtility.GenerateSecureToken();
        var salt = TokenUtility.GenerateSalt();
        var resetToken = new PasswordResetToken
        {
            UserId = user.Id,
            ExternalAppId = externalApp?.Id,
            ExternalUserId = externalApp is null ? null : externalUserId.Trim(),
            NormalizedExternalUserId = externalApp is null ? null : NormalizeExternalUserId(externalUserId),
            Salt = salt,
            ExpiresAt = expiresAt
        };
        var publicToken = BuildPublicResetToken(resetToken.Id, rawTokenSecret);
        resetToken.TokenHash = TokenUtility.HashToken(publicToken, salt);
        var resetUrl = BuildResetUrl(request.ResetUrlBase, user.Email ?? externalUserId, publicToken, externalApp);

        dbContext.PasswordResetTokens.Add(resetToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await notificationService.CreateAsync(new Notification
        {
            UserId = user.Id,
            Type = NotificationTypes.PasswordReset,
            Title = externalApp is null
                ? "Password reset requested"
                : $"Password reset requested by {externalApp.Name}",
            Message = $"Password reset requested by {externalApp?.Name ?? "Central Auth"}. The reset link was sent to your registered email and expires at {expiresAt:yyyy-MM-dd HH:mm} UTC.",
            SourceAppName = externalApp?.Name ?? "Central Auth",
            MetadataJson = JsonSerializer.Serialize(new
            {
                expiresAt,
                externalAppId = externalApp?.Id,
                externalAppName = externalApp?.Name,
                externalUserId
            })
        }, cancellationToken);

        await auditLogService.LogAsync(new AuditLog
        {
            Action = AuditActions.ForgotPasswordRequested,
            UserId = user.Id,
            ExternalAppId = externalApp?.Id,
            AppName = externalApp?.Name,
            ExternalUserId = externalUserId,
            PlatformEmail = user.Email
        }, cancellationToken);

        await emailSender.SendPasswordResetAsync(user, externalApp, resetUrl, expiresAt, cancellationToken);

        return ForgotPasswordStartResult.Accepted(new ForgotPasswordResponse(
            true,
            "تم إرسال طلب الاسترجاع. يرجى فتح منصة الدخول الموحد والتحقق من صندوق الإشعارات.",
            expiresAt));
    }

    private async Task<ApplicationUser?> ResolvePasswordResetUserAsync(
        string externalUserId,
        ExternalApp? externalApp,
        CancellationToken cancellationToken)
    {
        if (externalApp is null)
        {
            return await userManager.FindByEmailAsync(externalUserId.Trim());
        }

        var normalizedExternalUserId = NormalizeExternalUserId(externalUserId);
        var link = await dbContext.UserLinks
            .Include(userLink => userLink.PlatformUser)
            .FirstOrDefaultAsync(
                userLink =>
                    userLink.ExternalAppId == externalApp.Id &&
                    userLink.NormalizedExternalUserId == normalizedExternalUserId,
                cancellationToken);

        return link?.PlatformUser;
    }

    public async Task<ValidateResetTokenResponse> ValidateResetTokenAsync(
        ValidateResetTokenRequest request,
        CancellationToken cancellationToken)
    {
        var token = await FindTokenByEmailAndRawTokenAsync(request.Email, request.Token, cancellationToken);
        if (token is null)
        {
            return new ValidateResetTokenResponse(false, null);
        }

        var isValid = VerifyToken(token, request.Token);
        if (!isValid)
        {
            token.Attempts++;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new ValidateResetTokenResponse(isValid, token.ExpiresAt);
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var token = await FindTokenByEmailAndRawTokenAsync(request.Email, request.Token, cancellationToken);
        if (token?.User is null)
        {
            return false;
        }

        if (!VerifyToken(token, request.Token))
        {
            token.Attempts++;
            await dbContext.SaveChangesAsync(cancellationToken);
            return false;
        }

        var identityResetToken = await userManager.GeneratePasswordResetTokenAsync(token.User);
        var passwordResetResult = await userManager.ResetPasswordAsync(token.User, identityResetToken, request.NewPassword);
        if (!passwordResetResult.Succeeded)
        {
            return false;
        }

        var clearLockoutResult = await userManager.SetLockoutEndDateAsync(token.User, null);
        if (!clearLockoutResult.Succeeded)
        {
            return false;
        }

        var resetFailuresResult = await userManager.ResetAccessFailedCountAsync(token.User);
        if (!resetFailuresResult.Succeeded)
        {
            return false;
        }

        token.IsUsed = true;
        token.UsedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ValidateIntegrationResetTokenResponse> ValidateExternalResetTokenAsync(
        string rawToken,
        ExternalApp externalApp,
        CancellationToken cancellationToken)
    {
        var token = await FindExternalActiveTokenAsync(rawToken, externalApp.Id, cancellationToken);
        if (token is null)
        {
            return new ValidateIntegrationResetTokenResponse(false, null);
        }

        if (!VerifyToken(token, rawToken))
        {
            token.Attempts++;
            await dbContext.SaveChangesAsync(cancellationToken);
            return new ValidateIntegrationResetTokenResponse(false, null);
        }

        return new ValidateIntegrationResetTokenResponse(true, token.ExternalUserId);
    }

    public async Task<bool> MarkExternalResetTokenUsedAsync(
        string rawToken,
        ExternalApp externalApp,
        CancellationToken cancellationToken)
    {
        var token = await FindExternalActiveTokenAsync(rawToken, externalApp.Id, cancellationToken);
        if (token is null)
        {
            return false;
        }

        if (!VerifyToken(token, rawToken))
        {
            token.Attempts++;
            await dbContext.SaveChangesAsync(cancellationToken);
            return false;
        }

        token.IsUsed = true;
        token.UsedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<PasswordResetToken?> FindActiveTokenAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = userManager.NormalizeEmail(email.Trim());
        var now = DateTimeOffset.UtcNow;

        return await dbContext.PasswordResetTokens
            .Include(token => token.User)
            .Where(token =>
                token.User != null &&
                token.User.NormalizedEmail == normalizedEmail &&
                !token.IsUsed &&
                token.Attempts < MaxTokenAttempts &&
                token.ExpiresAt > now)
            .OrderByDescending(token => token.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<PasswordResetToken?> FindTokenByEmailAndRawTokenAsync(
        string email,
        string rawToken,
        CancellationToken cancellationToken)
    {
        if (TryGetResetTokenId(rawToken, out var tokenId))
        {
            var normalizedEmail = userManager.NormalizeEmail(email.Trim());
            var now = DateTimeOffset.UtcNow;

            return await dbContext.PasswordResetTokens
                .Include(token => token.User)
                .FirstOrDefaultAsync(token =>
                    token.Id == tokenId &&
                    token.User != null &&
                    token.User.NormalizedEmail == normalizedEmail &&
                    !token.IsUsed &&
                    token.Attempts < MaxTokenAttempts &&
                    token.ExpiresAt > now,
                    cancellationToken);
        }

        return await FindActiveTokenAsync(email, cancellationToken);
    }

    private async Task<PasswordResetToken?> FindExternalActiveTokenAsync(
        string rawToken,
        Guid externalAppId,
        CancellationToken cancellationToken)
    {
        if (!TryGetResetTokenId(rawToken, out var tokenId))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        return await dbContext.PasswordResetTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token =>
                token.Id == tokenId &&
                token.ExternalAppId == externalAppId &&
                !token.IsUsed &&
                token.Attempts < MaxTokenAttempts &&
                token.ExpiresAt > now,
                cancellationToken);
    }

    private static bool VerifyToken(PasswordResetToken token, string rawToken)
    {
        var candidateHash = TokenUtility.HashToken(rawToken, token.Salt);
        return TokenUtility.FixedTimeEquals(candidateHash, token.TokenHash);
    }

    private static string BuildResetUrl(string? resetUrlBase, string email, string rawToken, ExternalApp? externalApp)
    {
        var baseUrl = ResolveResetUrlBase(resetUrlBase, externalApp);

        var separator = baseUrl.Contains('?') ? '&' : '?';
        return externalApp is null
            ? $"{baseUrl}{separator}email={Uri.EscapeDataString(email.Trim())}&token={Uri.EscapeDataString(rawToken)}"
            : $"{baseUrl}{separator}token={Uri.EscapeDataString(rawToken)}";
    }

    private static string BuildPublicResetToken(Guid tokenId, string rawTokenSecret)
    {
        return $"{tokenId:N}.{rawTokenSecret}";
    }

    private static bool TryGetResetTokenId(string rawToken, out Guid tokenId)
    {
        tokenId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return false;
        }

        var separatorIndex = rawToken.IndexOf('.', StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            return false;
        }

        return Guid.TryParseExact(rawToken[..separatorIndex], "N", out tokenId);
    }

    private static string ResolveResetUrlBase(string? resetUrlBase, ExternalApp? externalApp)
    {
        if (externalApp is null || !Uri.TryCreate(externalApp.RedirectUri, UriKind.Absolute, out var registeredUri))
        {
            return string.IsNullOrWhiteSpace(resetUrlBase)
                ? "https://auth.example.com/reset-password"
                : resetUrlBase.Trim();
        }

        if (Uri.TryCreate(resetUrlBase?.Trim(), UriKind.Absolute, out var requestedUri) &&
            SameOrigin(registeredUri, requestedUri))
        {
            return requestedUri.ToString();
        }

        return registeredUri.ToString();
    }

    private static bool SameOrigin(Uri expected, Uri actual)
    {
        return string.Equals(expected.Scheme, actual.Scheme, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(expected.Host, actual.Host, StringComparison.OrdinalIgnoreCase) &&
            expected.Port == actual.Port;
    }

    private static string NormalizeExternalUserId(string externalUserId)
    {
        return externalUserId.Trim().ToUpperInvariant();
    }
}

public sealed record ForgotPasswordStartResult(
    ForgotPasswordResponse? Response,
    string? ErrorCode,
    string? ErrorMessage,
    int StatusCode)
{
    public bool Succeeded => Response is not null && StatusCode is >= 200 and < 300;

    public static ForgotPasswordStartResult Accepted(ForgotPasswordResponse response) =>
        new(response, null, null, StatusCodes.Status202Accepted);

    public static ForgotPasswordStartResult RateLimited(ForgotPasswordResponse response) =>
        new(response, "RESET_REQUEST_COOLDOWN", response.Message, StatusCodes.Status429TooManyRequests);

    public static ForgotPasswordStartResult NotFound() =>
        new(null, "ACCOUNT_LINK_NOT_FOUND", "No linked platform account exists for this external app and external user id.", StatusCodes.Status404NotFound);
}
