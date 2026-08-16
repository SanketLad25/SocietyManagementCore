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
}
