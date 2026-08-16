using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class BillingFrequencyRequest
{
    [Required, MaxLength(50)]
    public string Label { get; set; } = string.Empty;

    [Range(0, 60)]
    public int IntervalMonths { get; set; }

    public bool IsActive { get; set; } = true;
}
