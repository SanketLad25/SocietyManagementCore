using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class ComplaintAssignRequest
{
    [Required, MaxLength(100)]
    public string AssignedToName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? AssignedToContact { get; set; }

    [MaxLength(300)]
    public string? AssignmentNotes { get; set; }
}
