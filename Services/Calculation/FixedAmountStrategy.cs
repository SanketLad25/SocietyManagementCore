using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services.Calculation;

public class FixedAmountStrategy : ICalculationStrategy
{
    public string MethodCode => "FixedAmount";

    public decimal Calculate(MaintenanceChargeRule rule, FlatBillingContext context) => rule.Rate;
}
