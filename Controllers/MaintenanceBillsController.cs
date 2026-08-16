using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

/// A Resident's own flatId is always resolved server-side — the service ignores any flatId this
/// controller is asked to pass through for a Resident caller (mm §5).
[ApiController]
[Route("api/maintenance/bills")]
[Authorize]
public class MaintenanceBillsController(MaintenanceBillingService billingService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MaintenanceBillResponse>>> List([FromQuery] int? flatId)
    {
        return Ok(await billingService.ListBillsAsync(flatId));
    }

    [HttpGet("{billId:int}")]
    public async Task<ActionResult<MaintenanceBillResponse>> Get(int billId)
    {
        var bill = await billingService.GetBillAsync(billId);
        return bill is null ? NotFound(new { message = "Bill not found." }) : Ok(bill);
    }
}
