namespace SocietyManagementCore.Models.Dtos;

public class MaintenanceCategoryResponse
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? CategoryCode { get; set; }
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}
