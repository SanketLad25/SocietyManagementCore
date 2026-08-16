namespace SocietyManagementCore.Models.Entities;

public class AmenitySubscription : ITenantScoped
{
    public int SubscriptionId { get; set; }
    public int SocietyId { get; set; }
    public int AmenityId { get; set; }
    public int FlatId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
