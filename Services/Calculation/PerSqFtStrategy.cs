using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services.Calculation;

public class PerSqFtStrategy : ICalculationStrategy
{
    public string MethodCode => "PerSqFt";

    public decimal Calculate(MaintenanceChargeRule rule, FlatBillingContext context) =>
        rule.Rate * (context.Flat.AreaSqFt ?? 0);
}
