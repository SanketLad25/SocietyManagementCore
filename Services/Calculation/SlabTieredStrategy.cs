using System.Text.Json;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services.Calculation;

/// ParametersJson is a cumulative slab table over Flat.AreaSqFt, e.g.
/// [{"UpToSqFt":500,"RatePerSqFt":2},{"UpToSqFt":1000,"RatePerSqFt":3},{"UpToSqFt":null,"RatePerSqFt":4}]
/// — the portion of the area falling within each slab is charged at that slab's rate, and the
/// slabs are summed (same idea as progressive tax brackets). Rule.Rate is unused for this method.
public class SlabTieredStrategy : ICalculationStrategy
{
    private class Slab
    {
        public decimal? UpToSqFt { get; set; }
        public decimal RatePerSqFt { get; set; }
    }

    public string MethodCode => "SlabTiered";

    public decimal Calculate(MaintenanceChargeRule rule, FlatBillingContext context)
    {
        if (string.IsNullOrWhiteSpace(rule.ParametersJson))
        {
            return 0;
        }

        var slabs = JsonSerializer.Deserialize<List<Slab>>(rule.ParametersJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        if (slabs is null || slabs.Count == 0)
        {
            return 0;
        }

        var area = context.Flat.AreaSqFt ?? 0;
        var total = 0m;
        var previousThreshold = 0m;

        foreach (var slab in slabs)
        {
            if (area <= previousThreshold)
            {
                break;
            }

            var slabCeiling = slab.UpToSqFt ?? area;
            var portion = Math.Min(area, slabCeiling) - previousThreshold;

            if (portion > 0)
            {
                total += portion * slab.RatePerSqFt;
            }

            previousThreshold = slabCeiling;
        }

        return total;
    }
}
