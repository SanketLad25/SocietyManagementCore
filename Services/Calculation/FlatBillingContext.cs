using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services.Calculation;

/// Everything a calculation strategy needs to turn one MaintenanceChargeRule into an amount for
/// one flat. UnitCountResolver and CategoryAmountResolver are lazy (Func, not precomputed values)
/// since most strategies never touch them — only PerUnit and PercentageOfCategory do.
public class FlatBillingContext
{
    public required Flat Flat { get; init; }

    /// Takes a ParkingTypeId, returns how many parking slots of that type this flat has.
    /// Used by PerUnitStrategy.
    public required Func<int, int> UnitCountResolver { get; init; }

    /// Takes a CategoryId, returns the amount already computed for that category on this same
    /// flat within the same billing cycle. Used by PercentageOfCategoryStrategy — only meaningful
    /// once a billing cycle is actually generating line items (Phase 3).
    public required Func<int, decimal> CategoryAmountResolver { get; init; }
}
