namespace SocietyManagementCore.Models.Entities;

/// Composite-keyed (FlatGroupId, FlatId) join table — no surrogate id.
public class FlatGroupMember : ITenantScoped
{
    public int FlatGroupId { get; set; }
    public int FlatId { get; set; }
    public int SocietyId { get; set; }
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
