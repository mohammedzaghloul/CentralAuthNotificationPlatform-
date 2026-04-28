using CentralAuthNotificationPlatform.Dtos;
using CentralAuthNotificationPlatform.Extensions;
using CentralAuthNotificationPlatform.Models;
using CentralAuthNotificationPlatform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralAuthNotificationPlatform.Controllers;

[ApiController]
[Authorize(Roles = PlatformRoles.Admin)]
[Route("api/external-apps")]
public sealed class ExternalAppsController(IExternalAppService externalAppService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ExternalAppDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ExternalAppDto>>> GetMyApps(CancellationToken cancellationToken)
    {
        var apps = await externalAppService.GetOwnedAppsAsync(User.GetUserId(), cancellationToken);
        return Ok(apps);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreatedExternalAppDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateExternalAppRequest request, CancellationToken cancellationToken)
    {
        var result = await externalAppService.CreateAsync(User.GetUserId(), request, cancellationToken);
        if (result.Succeeded)
        {
            return Created($"/api/external-apps/{result.App!.Id}", result.App);
        }

        return StatusCode(result.StatusCode, new { error = new { code = result.ErrorCode, message = result.ErrorMessage } });
    }

    [HttpPost("{appId:guid}/api-key/regenerate")]
    [ProducesResponseType(typeof(RegeneratedApiKeyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegenerateApiKey(Guid appId, CancellationToken cancellationToken)
    {
        var result = await externalAppService.RegenerateApiKeyAsync(User.GetUserId(), appId, cancellationToken);
        return result is null
            ? NotFound(new { error = new { code = "APP_NOT_FOUND", message = "External app was not found." } })
            : Ok(result);
    }

    [HttpPost("{appId:guid}/api-key/revoke")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeApiKey(Guid appId, CancellationToken cancellationToken)
    {
        var revoked = await externalAppService.RevokeApiKeyAsync(User.GetUserId(), appId, cancellationToken);
        return revoked
            ? Ok(new MessageResponse("API key has been revoked."))
            : NotFound(new { error = new { code = "APP_NOT_FOUND", message = "External app was not found." } });
    }
}
