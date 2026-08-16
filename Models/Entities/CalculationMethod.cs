namespace SocietyManagementCore.Models.Entities;

/// Global, code-paired reference table (mm §2.1) — one row per ICalculationStrategy
/// implementation, matched by MethodCode. Not per-society: adding a new calculation method
/// requires a new strategy class, so it can't be configured away by an Admin.
public class CalculationMethod : IAuditable
{
    public int CalculationMethodId { get; set; }
    public string MethodCode { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ParametersSchema { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
