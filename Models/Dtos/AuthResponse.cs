namespace SocietyManagementCore.Models.Dtos;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; }

    /// Null for a SuperAdmin (not tied to any society).
    public int? SocietyId { get; set; }
    public string? SocietyName { get; set; }
}
