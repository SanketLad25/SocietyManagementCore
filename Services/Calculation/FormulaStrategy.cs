using NCalc;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services.Calculation;

/// ParametersJson holds a raw NCalc expression string (not JSON) — e.g. "Rate * AreaSqFt * 1.05".
/// This is the "no-code-change" escape hatch for a formula no built-in method covers (mm §11.5).
/// Evaluated through NCalc's sandboxed expression parser, never via reflection/eval on arbitrary
/// C#, so an Admin-authored formula can't reach outside the exposed variables.
public class FormulaStrategy : ICalculationStrategy
{
    public string MethodCode => "Formula";

    public decimal Calculate(MaintenanceChargeRule rule, FlatBillingContext context)
    {
        if (string.IsNullOrWhiteSpace(rule.ParametersJson))
        {
            return 0;
        }

        var expression = new Expression(rule.ParametersJson);
        expression.Parameters["Rate"] = (double)rule.Rate;
        expression.Parameters["AreaSqFt"] = (double)(context.Flat.AreaSqFt ?? 0);

        var result = expression.Evaluate();
        return Convert.ToDecimal(result);
    }

    /// Actually evaluates the expression against sample values — NCalc surfaces most syntax and
    /// undefined-variable errors only at Evaluate() time, not at Expression construction, so a
    /// parse-only check would miss exactly the failures this validation exists to catch.
    public (bool IsValid, string? Error) ValidateParameters(string? parametersJson)
    {
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return (false, "Formula requires ParametersJson: an NCalc expression, e.g. \"Rate * AreaSqFt\".");
        }

        try
        {
            var expression = new Expression(parametersJson);
            expression.Parameters["Rate"] = 0d;
            expression.Parameters["AreaSqFt"] = 0d;
            var result = expression.Evaluate();
            Convert.ToDecimal(result);
        }
        catch (Exception ex)
        {
            return (false, $"Formula is invalid: {ex.Message}");
        }

        return (true, null);
    }
}
