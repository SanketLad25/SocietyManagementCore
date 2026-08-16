namespace SocietyManagementCore.Models.Entities;

/// SocietyId is deliberately nullable: NULL marks a platform-level SuperAdmin account that
/// isn't tied to any one society. It is always set explicitly by the creating service (never
/// auto-stamped), since a SuperAdmin has no ambient SocietyId to fall back on. See ITenantScoped.
public class UserLogin : IAuditable
{
    public int UserId { get; set; }
    public int? ResidentId { get; set; }
    public int? SocietyId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int? RoleId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
