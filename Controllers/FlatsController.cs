using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/flats")]
[Authorize]
public class FlatsController(FlatService flatService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<FlatResponse>>> List([FromQuery] string? search)
    {
        return Ok(await flatService.ListAsync(search));
    }

    [HttpGet("{flatId:int}")]
    public async Task<ActionResult<FlatResponse>> Get(int flatId)
    {
        var flat = await flatService.GetAsync(flatId);
        return flat is null ? NotFound(new { message = "Flat not found." }) : Ok(flat);
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.CommitteeRoles)]
    public async Task<ActionResult<FlatResponse>> Create(FlatRequest request)
    {
        var result = await flatService.CreateAsync(request);
        if (!result.Success)
        {
            return Conflict(new { message = result.Error });
        }

        return CreatedAtAction(nameof(Get), new { flatId = result.Data!.FlatId }, result.Data);
    }

    [HttpPut("{flatId:int}")]
    [Authorize(Roles = RoleNames.CommitteeRoles)]
    public async Task<ActionResult<FlatResponse>> Update(int flatId, FlatRequest request)
    {
        var result = await flatService.UpdateAsync(flatId, request);
        if (!result.Success)
        {
            return result.Error == "Flat not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpDelete("{flatId:int}")]
    [Authorize(Roles = RoleNames.CommitteeRoles)]
    public async Task<IActionResult> Delete(int flatId)
    {
        var result = await flatService.DeleteAsync(flatId);
        if (!result.Success)
        {
            return result.Error == "Flat not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return NoContent();
    }
}
