using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/events")]
[Authorize]
public class EventsController(EventService eventService, EventRegistrationService registrationService) : ControllerBase
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

    [HttpPost("{eventId:int}/cancel")]
    [Authorize(Roles = RoleNames.NoticeManagerRoles)]
    public async Task<ActionResult<EventResponse>> Cancel(int eventId, EventCancelRequest request)
    {
        var result = await eventService.CancelAsync(eventId, request);
        if (!result.Success)
        {
            return result.Error == "Event not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpGet("{eventId:int}/registrations")]
    [Authorize(Roles = RoleNames.NoticeManagerRoles)]
    public async Task<ActionResult<List<EventRegistrationResponse>>> ListRegistrations(int eventId)
    {
        return Ok(await registrationService.ListForEventAsync(eventId));
    }

    [HttpPost("{eventId:int}/registrations/{registrationId:int}/promote")]
    [Authorize(Roles = RoleNames.NoticeManagerRoles)]
    public async Task<IActionResult> PromoteRegistration(int eventId, int registrationId)
    {
        var result = await registrationService.PromoteAsync(eventId, registrationId);
        return result.Success ? Ok() : Conflict(new { message = result.Error });
    }

    [HttpPost("{eventId:int}/interest")]
    [Authorize(Roles = RoleNames.Resident)]
    public async Task<IActionResult> MarkInterested(int eventId)
    {
        var result = await registrationService.MarkInterestedAsync(eventId);
        return result.Success ? Ok() : Conflict(new { message = result.Error });
    }

    [HttpPost("{eventId:int}/join")]
    [Authorize(Roles = RoleNames.Resident)]
    public async Task<IActionResult> Join(int eventId, EventRegistrationRequest request)
    {
        var result = await registrationService.JoinAsync(eventId, request);
        return result.Success ? Ok() : Conflict(new { message = result.Error });
    }

    [HttpPost("{eventId:int}/cancel-registration")]
    [Authorize(Roles = RoleNames.Resident)]
    public async Task<IActionResult> CancelRegistration(int eventId)
    {
        var result = await registrationService.CancelMyRegistrationAsync(eventId);
        return result.Success ? Ok() : Conflict(new { message = result.Error });
    }
}
