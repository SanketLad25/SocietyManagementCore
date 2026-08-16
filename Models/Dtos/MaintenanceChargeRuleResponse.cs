namespace SocietyManagementCore.Models.Dtos;

public class MaintenanceChargeRuleResponse
{
    public int RuleId { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int CalculationMethodId { get; set; }
    public string CalculationMethodName { get; set; } = string.Empty;
    public int ChargeTargetTypeId { get; set; }
    public string ChargeTargetTypeLabel { get; set; } = string.Empty;
    public int? TargetId { get; set; }
    public string? TargetLabel { get; set; }
    public decimal Rate { get; set; }
    public string? ParametersJson { get; set; }
    public int BillingFrequencyId { get; set; }
    public string BillingFrequencyLabel { get; set; } = string.Empty;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}
