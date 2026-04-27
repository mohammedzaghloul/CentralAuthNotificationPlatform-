using CentralAuthNotificationPlatform.Dtos;
using CentralAuthNotificationPlatform.Extensions;
using CentralAuthNotificationPlatform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralAuthNotificationPlatform.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(NotificationListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationListResponse>> GetInbox(CancellationToken cancellationToken)
    {
        return Ok(await notificationService.GetInboxAsync(User.GetUserId(), cancellationToken));
    }

    [HttpPost("read")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAsRead(MarkNotificationsReadRequest request, CancellationToken cancellationToken)
    {
        var count = await notificationService.MarkAsReadAsync(User.GetUserId(), request.NotificationIds, cancellationToken);
        return Ok(new MessageResponse($"{count} notification(s) marked as read."));
    }

    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkOneAsRead(Guid id, CancellationToken cancellationToken)
    {
        var count = await notificationService.MarkAsReadAsync(User.GetUserId(), [id], cancellationToken);
        return Ok(new MessageResponse($"{count} notification(s) marked as read."));
    }
}
