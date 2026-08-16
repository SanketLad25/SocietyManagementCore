using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

/// Username is deliberately not editable here — it's the login identifier and stays fixed after
/// creation, same as every other module treats its unique identifying field.
public class AdminUpdateUserRequest
{
    /// One of RoleNames.SocietyAssignableRoles.
    [Required]
    public string RoleName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    // Only meaningful when the user is linked to a Resident profile — ignored otherwise.
    [MaxLength(100)]
    public string? FullName { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(15)]
    public string? Mobile { get; set; }
}
