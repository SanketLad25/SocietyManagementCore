namespace SocietyManagementCore.Models.Entities;

public class ParkingType : ITenantScoped
{
    public int ParkingTypeId { get; set; }
    public int SocietyId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public int? DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
