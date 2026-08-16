using System.Security.Claims;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

public class CurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public int? UserId =>
        int.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? RoleName => User?.FindFirstValue(ClaimTypes.Role);

    public int? SocietyId =>
        int.TryParse(User?.FindFirstValue("societyId"), out var id) ? id : null;

    public int? ResidentId =>
        int.TryParse(User?.FindFirstValue("residentId"), out var id) ? id : null;

    public bool IsSuperAdmin => RoleName == RoleNames.SuperAdmin;
}
