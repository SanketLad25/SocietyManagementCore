using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class MaintenanceExemptionRequest
{
    [Required]
    public int CategoryId { get; set; }

    [Required]
    public int ChargeTargetTypeId { get; set; }

    public int? TargetId { get; set; }

    [MaxLength(300)]
    public string? Reason { get; set; }

    [Required]
    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }
}
