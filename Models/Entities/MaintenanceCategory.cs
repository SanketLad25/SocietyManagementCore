namespace SocietyManagementCore.Models.Entities;

public class MaintenanceCategory : ITenantScoped
{
    public int CategoryId { get; set; }
    public int SocietyId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? CategoryCode { get; set; }
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
