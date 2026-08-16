namespace SocietyManagementCore.Services;

/// The one place services read "who is making this request" from — backed by the JWT claims
/// on the current HTTP request. Avoids every service re-parsing ClaimsPrincipal itself.
public interface ICurrentUserContext
{
    int? UserId { get; }
    string? RoleName { get; }

    /// Null for a SuperAdmin (platform-level, not tied to any society).
    int? SocietyId { get; }

    /// Null unless this login is linked to a Resident profile (see UserLogin.ResidentId).
    int? ResidentId { get; }

    bool IsSuperAdmin { get; }
}
