using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

public class MaintenanceChargeRuleRequest
{
    [Required]
    public int CategoryId { get; set; }

    [Required]
    public int CalculationMethodId { get; set; }

    [Required]
    public int ChargeTargetTypeId { get; set; }

    /// Required unless the target type is AllFlats — validated in the service, not here, since
    /// the rule depends on which target type was chosen.
    public int? TargetId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Rate { get; set; }

    public string? ParametersJson { get; set; }

    [Required]
    public int BillingFrequencyId { get; set; }

    [Required]
    public DateOnly EffectiveFrom { get; set; }
}
