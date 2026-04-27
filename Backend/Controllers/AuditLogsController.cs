using CentralAuthNotificationPlatform.Dtos;
using CentralAuthNotificationPlatform.Extensions;
using CentralAuthNotificationPlatform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralAuthNotificationPlatform.Controllers;

[ApiController]
[Authorize]
[Route("api/audit-logs")]
public sealed class AuditLogsController(IAuditLogService auditLogService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(AuditLogListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuditLogListResponse>> GetMine(CancellationToken cancellationToken)
    {
        return Ok(await auditLogService.GetForUserAsync(User.GetUserId(), cancellationToken));
    }
}
