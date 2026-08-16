using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class FlatGroupRequest
{
    [Required, MaxLength(100)]
    public string GroupName { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}
