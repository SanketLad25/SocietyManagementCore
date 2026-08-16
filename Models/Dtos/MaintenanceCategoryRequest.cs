using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class MaintenanceCategoryRequest
{
    [Required, MaxLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? CategoryCode { get; set; }

    [MaxLength(300)]
    public string? Description { get; set; }

    public int? DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
