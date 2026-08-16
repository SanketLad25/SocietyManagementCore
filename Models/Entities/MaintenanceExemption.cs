namespace SocietyManagementCore.Models.Entities;

public class MaintenanceExemption : ITenantScoped
{
    public int ExemptionId { get; set; }
    public int SocietyId { get; set; }
    public int CategoryId { get; set; }
    public int ChargeTargetTypeId { get; set; }
    public int? TargetId { get; set; }
    public string? Reason { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
