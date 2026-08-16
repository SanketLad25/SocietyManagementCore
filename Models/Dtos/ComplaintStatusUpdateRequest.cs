using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class ComplaintStatusUpdateRequest
{
    [Required]
    public string NewStatus { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Comment { get; set; }
}
