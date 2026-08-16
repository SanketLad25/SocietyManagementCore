namespace SocietyManagementCore.Models.Entities;

public class BillingFrequency : ITenantScoped
{
    public int BillingFrequencyId { get; set; }
    public int SocietyId { get; set; }
    public string Label { get; set; } = string.Empty;
    public int IntervalMonths { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
