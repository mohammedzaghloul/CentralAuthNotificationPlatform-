using CentralAuthNotificationPlatform.Dtos;
using CentralAuthNotificationPlatform.Extensions;
using CentralAuthNotificationPlatform.Middleware;
using CentralAuthNotificationPlatform.Models;
using CentralAuthNotificationPlatform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CentralAuthNotificationPlatform.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService authService,
    SignInManager<ApplicationUser> signInManager) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var response = await authService.RegisterAsync(request, cancellationToken);
        if (response is null)
        {
            return Conflict(new { error = new { code = "REGISTRATION_FAILED", message = "Registration failed or email already exists." } });
        }

        return Created("/api/auth/login", response);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await authService.LoginAsync(request, cancellationToken);
        if (response is null)
        {
            return Unauthorized(new { error = new { code = "INVALID_CREDENTIALS", message = "Email or password is invalid." } });
        }

        return Ok(response);
    }

    [HttpGet("session")]
    [Authorize(AuthenticationSchemes = "Identity.Application,Bearer")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Session(CancellationToken cancellationToken)
    {
        var response = await authService.GetSessionAsync(User.GetUserId(), cancellationToken);
        return response is null
            ? Unauthorized(new { error = new { code = "SESSION_EXPIRED", message = "Session is expired." } })
            : Ok(response);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return Ok(new MessageResponse("Signed out."));
    }

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

    [HttpPost("forgot-password/validate")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ValidateResetTokenResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ValidateResetTokenResponse>> ValidateResetToken(
        ValidateResetTokenRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await authService.ValidateResetTokenAsync(request, cancellationToken));
    }

    [HttpPost("forgot-password/reset")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var success = await authService.ResetPasswordAsync(request, cancellationToken);
        if (!success)
        {
            return BadRequest(new { error = new { code = "INVALID_OR_EXPIRED_TOKEN", message = "Reset token is invalid or expired." } });
        }

        return Ok(new MessageResponse("Password has been reset successfully."));
    }
}
