using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class VisitorCategoryRequest
{
    [Required, MaxLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    public bool RequiresVehicleNo { get; set; }

    public bool RequiresCompanyName { get; set; }

    public bool RequiresApprovalDefault { get; set; } = true;

    public int? DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
