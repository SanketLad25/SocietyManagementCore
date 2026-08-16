using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/societies")]
[Authorize(Roles = RoleNames.SuperAdmin)]
public class SocietiesController(SocietyService societyService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SocietyResponse>>> List()
    {
        return Ok(await societyService.ListAsync());
    }

    [HttpGet("{societyId:int}")]
    public async Task<ActionResult<SocietyResponse>> Get(int societyId)
    {
        var society = await societyService.GetAsync(societyId);
        return society is null ? NotFound(new { message = "Society not found." }) : Ok(society);
    }

    [HttpPost]
    public async Task<ActionResult<SocietyResponse>> Create(SocietyRequest request)
    {
        var result = await societyService.CreateAsync(request);
        if (!result.Success)
        {
            return Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpPut("{societyId:int}")]
    public async Task<ActionResult<SocietyResponse>> Update(int societyId, SocietyRequest request)
    {
        var result = await societyService.UpdateAsync(societyId, request);
        if (!result.Success)
        {
            return result.Error == "Society not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpPost("{societyId:int}/admin")]
    public async Task<ActionResult<AdminUserResponse>> CreateFirstAdmin(int societyId, AdminCreateUserRequest request)
    {
        var result = await societyService.CreateFirstAdminAsync(societyId, request);
        if (!result.Success)
        {
            return result.Error == "Society not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }
}
