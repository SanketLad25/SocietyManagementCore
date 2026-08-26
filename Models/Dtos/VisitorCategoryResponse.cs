namespace SocietyManagementCore.Models.Dtos;

public class VisitorCategoryResponse
{
    public int VisitorCategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public bool RequiresVehicleNo { get; set; }
    public bool RequiresCompanyName { get; set; }
    public bool RequiresApprovalDefault { get; set; }
    public int? DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}
