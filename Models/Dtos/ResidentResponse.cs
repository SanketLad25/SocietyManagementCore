namespace SocietyManagementCore.Models.Dtos;

public class ResidentResponse
{
    public int ResidentId { get; set; }
    public int? FlatId { get; set; }
    public string? FlatNo { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public bool IsOwner { get; set; }
    public bool IsActive { get; set; }
}
