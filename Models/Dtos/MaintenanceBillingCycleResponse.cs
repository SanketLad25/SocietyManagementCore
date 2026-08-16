namespace SocietyManagementCore.Models.Dtos;

public class MaintenanceBillingCycleResponse
{
    public int CycleId { get; set; }
    public string CycleLabel { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateOnly DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? GeneratedOn { get; set; }
}
