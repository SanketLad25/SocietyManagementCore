namespace SocietyManagementCore.Models.Dtos;

public class AdminUserResponse
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public int? ResidentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Mobile { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
}
