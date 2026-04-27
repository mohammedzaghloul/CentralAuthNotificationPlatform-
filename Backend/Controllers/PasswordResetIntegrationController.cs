using CentralAuthNotificationPlatform.Dtos;
using CentralAuthNotificationPlatform.Middleware;
using CentralAuthNotificationPlatform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CentralAuthNotificationPlatform.Controllers;

[ApiController]
[Route("api/integration")]
[EnableRateLimiting("external-app")]
public sealed class PasswordResetIntegrationController(IAuthService authService) : ControllerBase
{
    [HttpPost("validate-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ValidateIntegrationResetTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ValidateIntegrationResetTokenResponse>> ValidateToken(
        ValidateIntegrationResetTokenRequest request,
        CancellationToken cancellationToken)
    {
        var externalApp = ApiKeyAuthenticationMiddleware.GetExternalApp(HttpContext);
        if (externalApp is null)
        {
            return Unauthorized(new { error = new { code = "INVALID_API_KEY", message = "A valid X-API-Key header is required." } });
        }

        return Ok(await authService.ValidateExternalResetTokenAsync(request.Token, externalApp, cancellationToken));
    }

    [HttpPost("mark-token-used")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkTokenUsed(
        MarkIntegrationResetTokenUsedRequest request,
        CancellationToken cancellationToken)
    {
        var externalApp = ApiKeyAuthenticationMiddleware.GetExternalApp(HttpContext);
        if (externalApp is null)
        {
            return Unauthorized(new { error = new { code = "INVALID_API_KEY", message = "A valid X-API-Key header is required." } });
        }

        var success = await authService.MarkExternalResetTokenUsedAsync(request.Token, externalApp, cancellationToken);
        if (!success)
        {
            return BadRequest(new
            {
                error = new
                {
                    code = "INVALID_OR_EXPIRED_TOKEN",
                    message = "Reset token is invalid, expired, or already used."
                }
            });
        }

        return Ok(new MessageResponse("Reset token marked as used."));
    }
}
