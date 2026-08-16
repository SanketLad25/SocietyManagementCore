using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class AdminCreateUserRequest
{
    [Required, MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    /// One of RoleNames.SocietyAssignableRoles.
    [Required]
    public string RoleName { get; set; } = string.Empty;

    // Only used when RoleName is Resident — links the new login to a resident profile.
    [MaxLength(100)]
    public string? FullName { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(15)]
    public string? Mobile { get; set; }

    public int? FlatId { get; set; }

    public bool IsOwner { get; set; }
}
