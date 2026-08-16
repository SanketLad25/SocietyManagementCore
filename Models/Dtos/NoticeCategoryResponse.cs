namespace SocietyManagementCore.Models.Dtos;

public class NoticeCategoryResponse
{
    public int NoticeCategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int? DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}
