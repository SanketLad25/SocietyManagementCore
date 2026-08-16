using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

/// Viewing cycles is open to every committee role; generating/publishing/previewing is narrower
/// (RoleNames.BillingRoles) — the view-vs-action split from mm §8.
[ApiController]
[Route("api/maintenance/billing-cycles")]
[Authorize(Roles = RoleNames.CommitteeRoles)]
public class MaintenanceBillingCyclesController(MaintenanceBillingService billingService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MaintenanceBillingCycleResponse>>> List()
    {
        return Ok(await billingService.ListCyclesAsync());
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.BillingRoles)]
    public async Task<ActionResult<MaintenanceBillingCycleResponse>> Create(MaintenanceBillingCycleRequest request)
    {
        var result = await billingService.CreateCycleAsync(request);
        return result.Success ? Ok(result.Data) : Conflict(new { message = result.Error });
    }

    [HttpGet("{cycleId:int}/preview")]
    [Authorize(Roles = RoleNames.BillingRoles)]
    public async Task<ActionResult<BillPreviewResponse>> Preview(int cycleId, [FromQuery] int flatId)
    {
        var result = await billingService.PreviewAsync(cycleId, flatId);
        if (!result.Success)
        {
            return result.Error == "Billing cycle not found." || result.Error == "Flat not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpPost("{cycleId:int}/generate")]
    [Authorize(Roles = RoleNames.BillingRoles)]
    public async Task<ActionResult<List<MaintenanceBillResponse>>> Generate(int cycleId)
    {
        var result = await billingService.GenerateAsync(cycleId);
        if (!result.Success)
        {
            return result.Error == "Billing cycle not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpPost("{cycleId:int}/publish")]
    [Authorize(Roles = RoleNames.BillingRoles)]
    public async Task<IActionResult> Publish(int cycleId)
    {
        var result = await billingService.PublishAsync(cycleId);
        if (!result.Success)
        {
            return result.Error == "Billing cycle not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok();
    }
}
