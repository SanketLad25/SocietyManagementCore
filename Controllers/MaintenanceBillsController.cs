using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

/// A Resident's own flatId is always resolved server-side — the service ignores any flatId this
/// controller is asked to pass through for a Resident caller (mm §5).
///
/// Bug fix (code review, 2026-08-26): was bare [Authorize], so any authenticated role — including
/// Security, a real assignable role with no legitimate reason to see financial data — could hit
/// this endpoint and fall into MaintenanceBillingService's "non-Resident" branch, which returns
/// every flat's bills unscoped. RoleNames.MaintenanceBillViewRoles restricts this to the roles the
/// service actually has a scoping story for: CommitteeRoles (see everything) and Resident (scoped
/// to their own flat).
[ApiController]
[Route("api/maintenance/bills")]
[Authorize(Roles = RoleNames.MaintenanceBillViewRoles)]
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
