namespace SocietyManagementCore.Models.Dtos;

public class MaintenanceExemptionResponse
{
    public int ExemptionId { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int ChargeTargetTypeId { get; set; }
    public string ChargeTargetTypeLabel { get; set; } = string.Empty;
    public int? TargetId { get; set; }
    public string? Reason { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}
