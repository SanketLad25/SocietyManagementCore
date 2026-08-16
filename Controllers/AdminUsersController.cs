using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/admin/users")]
public class AdminUsersController(AdminUserService adminUserService) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<List<AdminUserResponse>>> List()
    {
        return Ok(await adminUserService.ListAsync());
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<AdminUserResponse>> Create(AdminCreateUserRequest request)
    {
        var result = await adminUserService.CreateAsync(request);
        if (!result.Success)
        {
            return Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpPut("{userId:int}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<AdminUserResponse>> Update(int userId, AdminUpdateUserRequest request)
    {
        var result = await adminUserService.UpdateUserAsync(userId, request);
        if (!result.Success)
        {
            return result.Error == "User not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok(result.Data);
    }

    [HttpPost("{userId:int}/reset-password")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> ResetPassword(int userId, AdminResetPasswordRequest request)
    {
        var result = await adminUserService.ResetPasswordAsync(userId, request);
        if (!result.Success)
        {
            return result.Error == "User not found."
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }

        return Ok();
    }
}
