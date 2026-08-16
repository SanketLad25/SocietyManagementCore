using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/parking")]
[Authorize]
public class ParkingController(ParkingService parkingService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ParkingResponse>>> List([FromQuery] string? search)
    {
        return Ok(await parkingService.ListAsync(search));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.CommitteeRoles)]
    public async Task<ActionResult<ParkingResponse>> Create(ParkingRequest request)
    {
        var result = await parkingService.CreateAsync(request);
        if (!result.Success)
        {
            return Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpPut("{parkingId:int}")]
    [Authorize(Roles = RoleNames.CommitteeRoles)]
    public async Task<ActionResult<ParkingResponse>> Update(int parkingId, ParkingRequest request)
    {
        var result = await parkingService.UpdateAsync(parkingId, request);
        if (!result.Success)
        {
            return result.Error == "Parking record not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpDelete("{parkingId:int}")]
    [Authorize(Roles = RoleNames.CommitteeRoles)]
    public async Task<IActionResult> Delete(int parkingId)
    {
        var result = await parkingService.DeleteAsync(parkingId);
        if (!result.Success)
        {
            return result.Error == "Parking record not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return NoContent();
    }
}
