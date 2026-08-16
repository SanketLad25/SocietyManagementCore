using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

/// Reads restricted to committee roles (mm §5) — a blanket-open read would let any Resident
/// enumerate other flats' exemption reasons.
[ApiController]
[Route("api/maintenance/exemptions")]
[Authorize(Roles = RoleNames.CommitteeRoles)]
public class MaintenanceExemptionsController(MaintenanceExemptionService exemptionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MaintenanceExemptionResponse>>> List([FromQuery] int? categoryId)
    {
        return Ok(await exemptionService.ListAsync(categoryId));
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<MaintenanceExemptionResponse>> Create(MaintenanceExemptionRequest request)
    {
        var result = await exemptionService.CreateAsync(request);
        return result.Success ? Ok(result.Data) : Conflict(new { message = result.Error });
    }

    [HttpPut("{exemptionId:int}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<MaintenanceExemptionResponse>> Update(int exemptionId, MaintenanceExemptionRequest request)
    {
        var result = await exemptionService.UpdateAsync(exemptionId, request);
        if (!result.Success)
        {
            return result.Error == "Exemption not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }
}
