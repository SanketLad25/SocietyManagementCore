namespace SocietyManagementCore.Services.Calculation;

public class CalculationStrategyFactory(IEnumerable<ICalculationStrategy> strategies)
{
    public ICalculationStrategy Resolve(string methodCode)
    {
        var strategy = strategies.FirstOrDefault(s => s.MethodCode == methodCode);
        if (strategy is null)
        {
            throw new InvalidOperationException($"No calculation strategy registered for method code '{methodCode}'.");
        }

        return strategy;
    }
}
