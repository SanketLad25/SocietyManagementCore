namespace SocietyManagementCore.Models.Dtos;

public class ComplaintCategoryResponse
{
    public int ComplaintCategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int? DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}
