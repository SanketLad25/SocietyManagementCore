using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/maintenance/billing-frequencies")]
[Authorize]
public class BillingFrequenciesController(BillingFrequencyService billingFrequencyService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<BillingFrequencyResponse>>> List()
    {
        return Ok(await billingFrequencyService.ListAsync());
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<BillingFrequencyResponse>> Create(BillingFrequencyRequest request)
    {
        var result = await billingFrequencyService.CreateAsync(request);
        return result.Success ? Ok(result.Data) : Conflict(new { message = result.Error });
    }

    [HttpPut("{billingFrequencyId:int}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<BillingFrequencyResponse>> Update(int billingFrequencyId, BillingFrequencyRequest request)
    {
        var result = await billingFrequencyService.UpdateAsync(billingFrequencyId, request);
        if (!result.Success)
        {
            return result.Error == "Billing frequency not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }
}
