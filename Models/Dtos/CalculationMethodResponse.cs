namespace SocietyManagementCore.Models.Dtos;

public class CalculationMethodResponse
{
    public int CalculationMethodId { get; set; }
    public string MethodCode { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public string? Description { get; set; }
}
