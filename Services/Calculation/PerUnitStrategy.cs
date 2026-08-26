using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services.Calculation;

/// E.g. a Parking Maintenance rule targeting a specific ParkingType charges per slot — a flat
/// with two Two-Wheeler slots pays double a flat with one.
public class PerUnitStrategy : ICalculationStrategy
{
    public string MethodCode => "PerUnit";

    public decimal Calculate(MaintenanceChargeRule rule, FlatBillingContext context) =>
        rule.Rate * context.UnitCountResolver(rule.TargetId ?? 0);

    /// ParametersJson is unused by this method (it keys off Rule.TargetId instead) — nothing to validate.
    public (bool IsValid, string? Error) ValidateParameters(string? parametersJson) => (true, null);
}
