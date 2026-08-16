namespace SocietyManagementCore.Models.Entities;

/// Rates are never edited in place — revising a rule closes the current row's EffectiveTo and
/// inserts a new versioned row, so past bills stay computed against the rate that was actually
/// in effect at the time (mm §2.2).
public class MaintenanceChargeRule : ITenantScoped
{
    public int RuleId { get; set; }
    public int SocietyId { get; set; }
    public int CategoryId { get; set; }
    public int CalculationMethodId { get; set; }
    public int ChargeTargetTypeId { get; set; }

    /// Polymorphic, resolved via ChargeTargetTypeId — not FK-enforced (mm §2.2), validated by
    /// MaintenanceTargetResolver in the service layer.
    public int? TargetId { get; set; }

    public decimal Rate { get; set; }
    public string? ParametersJson { get; set; }
    public int BillingFrequencyId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
