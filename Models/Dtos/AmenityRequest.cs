using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class AmenityRequest
{
    [Required, MaxLength(100)]
    public string AmenityName { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Description { get; set; }

    public bool RequiresOptIn { get; set; }

    public int? DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
