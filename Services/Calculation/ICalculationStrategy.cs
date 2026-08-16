using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services.Calculation;

public interface ICalculationStrategy
{
    /// Matches CalculationMethod.MethodCode exactly (mm §3).
    string MethodCode { get; }

    decimal Calculate(MaintenanceChargeRule rule, FlatBillingContext context);
}
