using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/maintenance/amenities")]
[Authorize]
public class AmenitiesController(AmenityService amenityService, AmenitySubscriptionService subscriptionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AmenityResponse>>> List()
    {
        return Ok(await amenityService.ListAsync());
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<AmenityResponse>> Create(AmenityRequest request)
    {
        var result = await amenityService.CreateAsync(request);
        return result.Success ? Ok(result.Data) : Conflict(new { message = result.Error });
    }

    [HttpPut("{amenityId:int}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<AmenityResponse>> Update(int amenityId, AmenityRequest request)
    {
        var result = await amenityService.UpdateAsync(amenityId, request);
        if (!result.Success)
        {
            return result.Error == "Amenity not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpGet("{amenityId:int}/subscriptions")]
    [Authorize(Roles = RoleNames.CommitteeRoles)]
    public async Task<ActionResult<List<AmenitySubscriptionResponse>>> ListSubscriptions(int amenityId)
    {
        return Ok(await subscriptionService.ListAsync(amenityId));
    }

    [HttpPost("{amenityId:int}/subscriptions")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<AmenitySubscriptionResponse>> CreateSubscription(int amenityId, AmenitySubscriptionRequest request)
    {
        var result = await subscriptionService.CreateAsync(amenityId, request);
        return result.Success ? Ok(result.Data) : Conflict(new { message = result.Error });
    }

    [HttpPost("{amenityId:int}/subscriptions/{subscriptionId:int}/cancel")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> CancelSubscription(int amenityId, int subscriptionId, [FromBody] CancelAmenitySubscriptionRequest request)
    {
        var result = await subscriptionService.CancelAsync(amenityId, subscriptionId, request.EffectiveTo);
        if (!result.Success)
        {
            return result.Error == "Subscription not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok();
    }
}
