using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services.Calculation;

public class FixedAmountStrategy : ICalculationStrategy
{
    public string MethodCode => "FixedAmount";

    public decimal Calculate(MaintenanceChargeRule rule, FlatBillingContext context) => rule.Rate;

    /// ParametersJson is unused by this method — nothing to validate.
    public (bool IsValid, string? Error) ValidateParameters(string? parametersJson) => (true, null);
}
