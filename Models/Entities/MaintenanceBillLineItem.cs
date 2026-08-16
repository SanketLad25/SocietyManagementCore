namespace SocietyManagementCore.Models.Entities;

/// Immutable snapshot row — implements neither ITenantScoped nor IAuditable (mm §2.3/§2.5, same
/// exception as AuditLog). Once a bill is generated its line items are never edited, only
/// superseded by re-running generation, so there's no ModifiedOn/ModifiedBy to stamp.
public class MaintenanceBillLineItem
{
    public int LineItemId { get; set; }
    public int SocietyId { get; set; }
    public int BillId { get; set; }
    public int? RuleId { get; set; }
    public int CategoryId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedOn { get; set; }
}
