namespace SocietyManagementCore.Models.Entities;

/// Global reference table (mm §2.1) — the fixed set of ways a charge can be targeted
/// (AllFlats/Wing/FlatGroup/SpecificFlat/ParkingType/Amenity). Per-society enablement of these
/// is a separate concern, see SocietyChargeTargetType.
public class ChargeTargetType : IAuditable
{
    public int ChargeTargetTypeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
