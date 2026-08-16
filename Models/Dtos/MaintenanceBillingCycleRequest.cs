using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class MaintenanceBillingCycleRequest
{
    [Required, MaxLength(50)]
    public string CycleLabel { get; set; } = string.Empty;

    [Required]
    public DateOnly PeriodStart { get; set; }

    [Required]
    public DateOnly PeriodEnd { get; set; }

    [Required]
    public DateOnly DueDate { get; set; }
}
