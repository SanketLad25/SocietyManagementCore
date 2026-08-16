using System.ComponentModel.DataAnnotations;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Models.Dtos;

public class ComplaintRequest
{
    [Required]
    public int CategoryId { get; set; }

    [Required, MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public string Priority { get; set; } = ComplaintPriorities.Medium;
}
