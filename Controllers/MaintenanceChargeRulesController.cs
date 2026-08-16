using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/maintenance/charge-rules")]
[Authorize]
public class MaintenanceChargeRulesController(MaintenanceChargeRuleService chargeRuleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MaintenanceChargeRuleResponse>>> List([FromQuery] int? categoryId)
    {
        return Ok(await chargeRuleService.ListAsync(categoryId));
    }

    [HttpGet("my-flat")]
    public async Task<ActionResult<List<MyChargeResponse>>> MyFlat()
    {
        return Ok(await chargeRuleService.GetMyChargesAsync());
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<MaintenanceChargeRuleResponse>> Create(MaintenanceChargeRuleRequest request)
    {
        var result = await chargeRuleService.CreateAsync(request);
        return result.Success ? Ok(result.Data) : Conflict(new { message = result.Error });
    }

    [HttpPost("{ruleId:int}/revise")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<MaintenanceChargeRuleResponse>> Revise(int ruleId, ReviseChargeRuleRequest request)
    {
        var result = await chargeRuleService.ReviseAsync(ruleId, request);
        if (!result.Success)
        {
            return result.Error == "Charge rule not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }
}
