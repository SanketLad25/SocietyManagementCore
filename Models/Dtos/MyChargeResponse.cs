namespace SocietyManagementCore.Models.Dtos;

/// One line of the Resident-facing "My Charges" view (mm §10) — plain-language, not a raw rule dump.
public class MyChargeResponse
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public string CalculationMethodName { get; set; } = string.Empty;
    public string BillingFrequencyLabel { get; set; } = string.Empty;
    public DateOnly EffectiveFrom { get; set; }
}
