using CentralAuthNotificationPlatform.BLL.Dtos;
using CentralAuthNotificationPlatform.PL.Extensions;
using CentralAuthNotificationPlatform.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentralAuthNotificationPlatform.PL.Controllers;

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
