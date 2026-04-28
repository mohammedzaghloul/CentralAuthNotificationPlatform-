using CentralAuthNotificationPlatform.Dtos;
using CentralAuthNotificationPlatform.Extensions;
using CentralAuthNotificationPlatform.Models;
using CentralAuthNotificationPlatform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralAuthNotificationPlatform.Controllers;

[ApiController]
[Authorize]
[Route("api/user-links")]
public sealed class UserLinksController(IUserLinkService userLinkService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserLinkDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserLinkDto>>> GetMyLinks(CancellationToken cancellationToken)
    {
        return Ok(await userLinkService.GetVisibleLinksAsync(User.GetUserId(), cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = PlatformRoles.Admin)]
    [ProducesResponseType(typeof(UserLinkDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateUserLinkRequest request, CancellationToken cancellationToken)
    {
        var result = await userLinkService.CreateAsync(User.GetUserId(), request, cancellationToken);
        if (result.Succeeded)
        {
            return Created($"/api/user-links/{result.Link!.Id}", result.Link);
        }

        var error = new { error = new { code = result.ErrorCode, message = result.ErrorMessage } };
        return result.ErrorCode == "LINK_EXISTS" ? Conflict(error) : BadRequest(error);
    }

    [HttpDelete("{linkId:guid}")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid linkId, CancellationToken cancellationToken)
    {
        var deleted = await userLinkService.DeleteVisibleLinkAsync(User.GetUserId(), linkId, cancellationToken);
        return deleted
            ? Ok(new MessageResponse("Account link removed."))
            : NotFound(new { error = new { code = "LINK_NOT_FOUND", message = "Account link was not found." } });
    }
}
