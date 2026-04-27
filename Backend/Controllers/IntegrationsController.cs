using CentralAuthNotificationPlatform.Dtos;
using CentralAuthNotificationPlatform.Middleware;
using CentralAuthNotificationPlatform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CentralAuthNotificationPlatform.Controllers;

[ApiController]
[Route("api/integrations")]
public sealed class IntegrationsController(
    IAuthService authService,
    IUserLinkService userLinkService) : ControllerBase
{
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("password-reset")]
    [ProducesResponseType(typeof(ForgotPasswordResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var externalApp = ApiKeyAuthenticationMiddleware.GetExternalApp(HttpContext);
        if (externalApp is null)
        {
            return Unauthorized(new { error = new { code = "INVALID_API_KEY", message = "A valid X-API-Key header is required." } });
        }

        var result = await authService.StartForgotPasswordAsync(request, externalApp, cancellationToken);
        if (result.Succeeded)
        {
            return Accepted(result.Response);
        }

        var error = new { error = new { code = result.ErrorCode, message = result.ErrorMessage } };
        return StatusCode(result.StatusCode, error);
    }

    [HttpPost("user-links")]
    [Authorize]
    [EnableRateLimiting("external-app")]
    [ProducesResponseType(typeof(UserLinkDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUserLink(
        CreateIntegrationUserLinkRequest request,
        CancellationToken cancellationToken)
    {
        var externalApp = ApiKeyAuthenticationMiddleware.GetExternalApp(HttpContext);
        if (externalApp is null)
        {
            return Unauthorized(new { error = new { code = "INVALID_API_KEY", message = "A valid X-API-Key header is required." } });
        }

        var userIdValue = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdValue, out var platformUserId))
        {
            return Unauthorized(new { error = new { code = "INVALID_ACCESS_TOKEN", message = "A valid platform access token is required." } });
        }

        var result = await userLinkService.CreateForExternalAppAsync(
            externalApp.Id,
            platformUserId,
            request.UserIdentifier,
            cancellationToken);

        if (result.Succeeded)
        {
            return Ok(result.Link);
        }

        var error = new { error = new { code = result.ErrorCode, message = result.ErrorMessage } };
        return result.ErrorCode == "LINK_EXISTS" ? Conflict(error) : BadRequest(error);
    }

    [HttpGet("user-links/status")]
    [AllowAnonymous]
    [EnableRateLimiting("external-app")]
    [ProducesResponseType(typeof(UserLinkStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUserLinkStatus(
        [FromQuery] string? externalUserId,
        [FromQuery] string? externalEmail,
        CancellationToken cancellationToken)
    {
        var externalApp = ApiKeyAuthenticationMiddleware.GetExternalApp(HttpContext);
        if (externalApp is null)
        {
            return Unauthorized(new { error = new { code = "INVALID_API_KEY", message = "A valid X-API-Key header is required." } });
        }

        var identifier = ResolveExternalUserId(externalUserId, externalEmail);
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "External user id is required." } });
        }

        return Ok(await userLinkService.GetForExternalAppAsync(externalApp.Id, identifier, cancellationToken));
    }

    [HttpDelete("user-links")]
    [AllowAnonymous]
    [EnableRateLimiting("external-app")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteUserLink(
        [FromQuery] string? externalUserId,
        [FromQuery] string? externalEmail,
        CancellationToken cancellationToken)
    {
        var externalApp = ApiKeyAuthenticationMiddleware.GetExternalApp(HttpContext);
        if (externalApp is null)
        {
            return Unauthorized(new { error = new { code = "INVALID_API_KEY", message = "A valid X-API-Key header is required." } });
        }

        var identifier = ResolveExternalUserId(externalUserId, externalEmail);
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "External user id is required." } });
        }

        var deleted = await userLinkService.DeleteForExternalAppAsync(externalApp.Id, identifier, cancellationToken);
        return Ok(new MessageResponse(deleted ? "Account link removed." : "No account link existed."));
    }

    private static string ResolveExternalUserId(string? externalUserId, string? externalEmail)
    {
        return string.IsNullOrWhiteSpace(externalUserId)
            ? externalEmail?.Trim() ?? string.Empty
            : externalUserId.Trim();
    }
}
