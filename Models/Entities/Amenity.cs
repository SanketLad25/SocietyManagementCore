namespace SocietyManagementCore.Models.Entities;

public class Amenity : ITenantScoped
{
    public int AmenityId { get; set; }
    public int SocietyId { get; set; }
    public string AmenityName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool RequiresOptIn { get; set; }
    public int? DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
