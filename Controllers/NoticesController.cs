using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/notices")]
[Authorize]
public class NoticesController(NoticeService noticeService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<NoticeResponse>>> List(
        [FromQuery] int? categoryId,
        [FromQuery] string? priority,
        [FromQuery] string? search,
        [FromQuery] string? sortBy,
        [FromQuery] bool includeDrafts = false)
    {
        return Ok(await noticeService.ListAsync(categoryId, priority, search, sortBy, includeDrafts));
    }

    [HttpGet("{noticeId:int}")]
    public async Task<ActionResult<NoticeResponse>> Get(int noticeId)
    {
        var notice = await noticeService.GetAsync(noticeId);
        return notice is null ? NotFound(new { message = "Notice not found." }) : Ok(notice);
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.NoticeManagerRoles)]
    public async Task<ActionResult<NoticeResponse>> Create(NoticeRequest request)
    {
        var result = await noticeService.CreateAsync(request);
        return result.Success ? Ok(result.Data) : Conflict(new { message = result.Error });
    }

    [HttpPut("{noticeId:int}")]
    [Authorize(Roles = RoleNames.NoticeManagerRoles)]
    public async Task<ActionResult<NoticeResponse>> Update(int noticeId, NoticeRequest request)
    {
        var result = await noticeService.UpdateAsync(noticeId, request);
        if (!result.Success)
        {
            return result.Error == "Notice not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpPost("{noticeId:int}/publish")]
    [Authorize(Roles = RoleNames.NoticeManagerRoles)]
    public async Task<ActionResult<NoticeResponse>> Publish(int noticeId)
    {
        var result = await noticeService.PublishAsync(noticeId);
        if (!result.Success)
        {
            return result.Error == "Notice not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpDelete("{noticeId:int}")]
    [Authorize(Roles = RoleNames.NoticeManagerRoles)]
    public async Task<IActionResult> Delete(int noticeId)
    {
        var result = await noticeService.DeleteAsync(noticeId);
        if (!result.Success)
        {
            return NotFound(new { message = result.Error });
        }

        return NoContent();
    }

    [HttpPost("{noticeId:int}/read")]
    public async Task<IActionResult> MarkRead(int noticeId)
    {
        var result = await noticeService.MarkReadAsync(noticeId);
        if (!result.Success)
        {
            return NotFound(new { message = result.Error });
        }

        return NoContent();
    }
}
