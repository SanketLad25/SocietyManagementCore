namespace SocietyManagementCore.Models.Entities;

public class VisitorCategory : ITenantScoped
{
    public int VisitorCategoryId { get; set; }
    public int SocietyId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public bool RequiresVehicleNo { get; set; }
    public bool RequiresCompanyName { get; set; }
    public bool RequiresApprovalDefault { get; set; } = true;
    public int? DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
