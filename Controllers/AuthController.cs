using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService, ICurrentUserContext currentUser) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var result = await authService.LoginAsync(request);
        if (!result.Success)
        {
            return Unauthorized(new { message = result.Error });
        }

        return Ok(result.Response);
    }

    /// Self-service — any authenticated role, including a user whose MustChangePassword flag is
    /// still set (the JWT they hold is already valid at that point, only the frontend routing gates
    /// them to this screen first).
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await authService.ChangePasswordAsync(userId, request);
        if (!result.Success)
        {
            return Conflict(new { message = result.Error });
        }

        return Ok();
    }
}
