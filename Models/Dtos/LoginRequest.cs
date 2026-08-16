using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class LoginRequest
{
    /// Left blank to log in as a SuperAdmin (platform-level account, not tied to a society).
    public string? SocietyName { get; set; }

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
