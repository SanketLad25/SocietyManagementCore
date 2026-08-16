using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/maintenance/parking-types")]
[Authorize]
public class ParkingTypesController(ParkingTypeService parkingTypeService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ParkingTypeResponse>>> List()
    {
        return Ok(await parkingTypeService.ListAsync());
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<ParkingTypeResponse>> Create(ParkingTypeRequest request)
    {
        var result = await parkingTypeService.CreateAsync(request);
        return result.Success ? Ok(result.Data) : Conflict(new { message = result.Error });
    }

    [HttpPut("{parkingTypeId:int}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<ParkingTypeResponse>> Update(int parkingTypeId, ParkingTypeRequest request)
    {
        var result = await parkingTypeService.UpdateAsync(parkingTypeId, request);
        if (!result.Success)
        {
            return result.Error == "Parking type not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }
}
