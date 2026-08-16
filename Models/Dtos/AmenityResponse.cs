namespace SocietyManagementCore.Models.Dtos;

public class AmenityResponse
{
    public int AmenityId { get; set; }
    public string AmenityName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool RequiresOptIn { get; set; }
    public int? DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}
