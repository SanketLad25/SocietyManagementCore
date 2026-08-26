using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services.Calculation;

public interface ICalculationStrategy
{
    /// Matches CalculationMethod.MethodCode exactly (mm §3).
    string MethodCode { get; }

    decimal Calculate(MaintenanceChargeRule rule, FlatBillingContext context);

    /// Sanity-checks ParametersJson against this method's expected shape at rule create/revise
    /// time (code review finding #2, 2026-08-26) — so a malformed SlabTiered/PercentageOfCategory
    /// payload or an invalid Formula expression is rejected with a friendly message on save,
    /// instead of throwing the first time Preview/Generate actually evaluates it.
    (bool IsValid, string? Error) ValidateParameters(string? parametersJson);
}
