namespace SocietyManagementCore.Models.Entities;

public class MaintenanceBillingCycle : ITenantScoped
{
    public int CycleId { get; set; }
    public int SocietyId { get; set; }
    public string CycleLabel { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateOnly DueDate { get; set; }

    /// "Draft" -> "Generated" -> "Published". Generation and publish are one-way (mm §6).
    public string Status { get; set; } = "Draft";

    public DateTime? GeneratedOn { get; set; }
    public int? GeneratedBy { get; set; }
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
