using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

/// Narrower than MaintenanceChargeRuleRequest — revising a rule can only change the rate, its
/// parameters, and when the new rate takes effect. Category/method/target/frequency stay fixed;
/// changing those is a new rule, not a revision.
public class ReviseChargeRuleRequest
{
    [Range(0, double.MaxValue)]
    public decimal Rate { get; set; }

    public string? ParametersJson { get; set; }

    [Required]
    public DateOnly EffectiveFrom { get; set; }
}
