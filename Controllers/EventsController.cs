using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/events")]
[Authorize]
public class EventsController(EventService eventService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<EventResponse>>> List([FromQuery] int? categoryId, [FromQuery] string? status)
    {
        return Ok(await eventService.ListAsync(categoryId, status));
    }

    [HttpGet("{eventId:int}")]
    public async Task<ActionResult<EventResponse>> Get(int eventId)
    {
        var ev = await eventService.GetAsync(eventId);
        return ev is null ? NotFound(new { message = "Event not found." }) : Ok(ev);
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.NoticeManagerRoles)]
    public async Task<ActionResult<EventResponse>> Create(EventRequest request)
    {
        var result = await eventService.CreateAsync(request);
        if (!result.Success)
        {
            return Conflict(new { message = result.Error });
        }

        return CreatedAtAction(nameof(Get), new { eventId = result.Data!.EventId }, result.Data);
    }

    [HttpPut("{eventId:int}")]
    [Authorize(Roles = RoleNames.NoticeManagerRoles)]
    public async Task<ActionResult<EventResponse>> Update(int eventId, EventRequest request)
    {
        var result = await eventService.UpdateAsync(eventId, request);
        if (!result.Success)
        {
            return result.Error == "Event not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }
}
