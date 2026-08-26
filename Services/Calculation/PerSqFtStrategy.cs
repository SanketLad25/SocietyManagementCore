using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services.Calculation;

public class PerSqFtStrategy : ICalculationStrategy
{
    public string MethodCode => "PerSqFt";

    public decimal Calculate(MaintenanceChargeRule rule, FlatBillingContext context) =>
        rule.Rate * (context.Flat.AreaSqFt ?? 0);

    /// ParametersJson is unused by this method — nothing to validate.
    public (bool IsValid, string? Error) ValidateParameters(string? parametersJson) => (true, null);
}
