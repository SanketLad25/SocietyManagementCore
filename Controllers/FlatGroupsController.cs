using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/maintenance/flat-groups")]
[Authorize]
public class FlatGroupsController(FlatGroupService flatGroupService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<FlatGroupResponse>>> List()
    {
        return Ok(await flatGroupService.ListAsync());
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<FlatGroupResponse>> Create(FlatGroupRequest request)
    {
        var result = await flatGroupService.CreateAsync(request);
        return result.Success ? Ok(result.Data) : Conflict(new { message = result.Error });
    }

    [HttpPut("{flatGroupId:int}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<FlatGroupResponse>> Update(int flatGroupId, FlatGroupRequest request)
    {
        var result = await flatGroupService.UpdateAsync(flatGroupId, request);
        if (!result.Success)
        {
            return result.Error == "Flat group not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpPost("{flatGroupId:int}/members")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> AddMember(int flatGroupId, FlatGroupMemberRequest request)
    {
        var result = await flatGroupService.AddMemberAsync(flatGroupId, request);
        if (!result.Success)
        {
            return result.Error == "Flat group not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok();
    }

    [HttpDelete("{flatGroupId:int}/members/{flatId:int}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> RemoveMember(int flatGroupId, int flatId)
    {
        var result = await flatGroupService.RemoveMemberAsync(flatGroupId, flatId);
        if (!result.Success)
        {
            return result.Error == "Flat group not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return NoContent();
    }
}
