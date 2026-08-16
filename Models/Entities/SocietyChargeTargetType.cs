namespace SocietyManagementCore.Models.Entities;

/// Per-society enable/disable toggle over the global ChargeTargetType list (mm §2.1) — lets a
/// society hide target types it never uses (e.g. no ParkingType if it has no parking) without
/// touching the global reference table.
public class SocietyChargeTargetType : ITenantScoped
{
    public int SocietyChargeTargetTypeId { get; set; }
    public int SocietyId { get; set; }
    public int ChargeTargetTypeId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
