using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/maintenance/charge-target-types")]
[Authorize]
public class ChargeTargetTypesController(ChargeTargetTypeService chargeTargetTypeService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ChargeTargetTypeResponse>>> List()
    {
        return Ok(await chargeTargetTypeService.ListAsync());
    }

    [HttpPut("{chargeTargetTypeId:int}/enablement")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> SetEnablement(int chargeTargetTypeId, SetChargeTargetTypeEnablementRequest request)
    {
        var result = await chargeTargetTypeService.SetEnablementAsync(chargeTargetTypeId, request.IsEnabled);
        if (!result.Success)
        {
            return result.Error == "Charge target type not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok();
    }
}
