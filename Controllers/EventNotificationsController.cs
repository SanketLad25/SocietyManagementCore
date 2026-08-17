using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/events/notifications")]
[Authorize]
public class EventNotificationsController(EventNotificationService notificationService) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<List<EventNotificationResponse>>> Mine()
    {
        return Ok(await notificationService.ListMineAsync());
    }

    [HttpPost("{notificationId:int}/read")]
    public async Task<IActionResult> MarkRead(int notificationId)
    {
        var result = await notificationService.MarkReadAsync(notificationId);
        if (!result.Success)
        {
            return NotFound(new { message = result.Error });
        }

        return NoContent();
    }
}
