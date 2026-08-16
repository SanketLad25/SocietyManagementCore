using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class ComplaintCategoryRequest
{
    [Required, MaxLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    public int? DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
