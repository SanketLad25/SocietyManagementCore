using System.Text.Json;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services.Calculation;

/// ParametersJson is {"BaseCategoryId": 5} — Rule.Rate is the percentage (10 = 10%) applied to
/// whatever amount was already computed for that base category, on this same flat, in this same
/// billing cycle. Only meaningful once a billing cycle exists (Phase 3) — CategoryAmountResolver
/// returns 0 outside that context.
public class PercentageOfCategoryStrategy : ICalculationStrategy
{
    private class Parameters
    {
        public int BaseCategoryId { get; set; }
    }

    public string MethodCode => "PercentageOfCategory";

    public decimal Calculate(MaintenanceChargeRule rule, FlatBillingContext context)
    {
        if (string.IsNullOrWhiteSpace(rule.ParametersJson))
        {
            return 0;
        }

        var parameters = JsonSerializer.Deserialize<Parameters>(rule.ParametersJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        if (parameters is null)
        {
            return 0;
        }

        var baseAmount = context.CategoryAmountResolver(parameters.BaseCategoryId);
        return baseAmount * (rule.Rate / 100m);
    }

    public (bool IsValid, string? Error) ValidateParameters(string? parametersJson)
    {
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return (false, "PercentageOfCategory requires ParametersJson: {\"BaseCategoryId\": <id>}.");
        }

        Parameters? parameters;
        try
        {
            parameters = JsonSerializer.Deserialize<Parameters>(parametersJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch (JsonException ex)
        {
            return (false, $"ParametersJson is not valid: {ex.Message}");
        }

        if (parameters is null || parameters.BaseCategoryId <= 0)
        {
            return (false, "ParametersJson must specify a valid BaseCategoryId.");
        }

        return (true, null);
    }
}
