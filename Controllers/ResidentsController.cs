using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/residents")]
[Authorize]
public class ResidentsController(ResidentService residentService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ResidentResponse>>> List([FromQuery] string? search)
    {
        return Ok(await residentService.ListAsync(search));
    }

    [HttpGet("{residentId:int}")]
    public async Task<ActionResult<ResidentResponse>> Get(int residentId)
    {
        var resident = await residentService.GetAsync(residentId);
        return resident is null ? NotFound(new { message = "Resident not found." }) : Ok(resident);
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.CommitteeRoles)]
    public async Task<ActionResult<ResidentResponse>> Create(ResidentRequest request)
    {
        var result = await residentService.CreateAsync(request);
        if (!result.Success)
        {
            return Conflict(new { message = result.Error });
        }

        return CreatedAtAction(nameof(Get), new { residentId = result.Data!.ResidentId }, result.Data);
    }

    [HttpPut("{residentId:int}")]
    [Authorize(Roles = RoleNames.CommitteeRoles)]
    public async Task<ActionResult<ResidentResponse>> Update(int residentId, ResidentRequest request)
    {
        var result = await residentService.UpdateAsync(residentId, request);
        if (!result.Success)
        {
            return result.Error == "Resident not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpDelete("{residentId:int}")]
    [Authorize(Roles = RoleNames.CommitteeRoles)]
    public async Task<IActionResult> Delete(int residentId)
    {
        var result = await residentService.DeleteAsync(residentId);
        if (!result.Success)
        {
            return result.Error == "Resident not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return NoContent();
    }
}
