namespace SocietyManagementCore.Models.Entities;

/// ITenantScoped/IAuditable — the one mutable/status-pipeline table in this module. A group visit
/// (VisitorManagement.md "Resolved decisions" #3) is tracked as one unit: one approval, one
/// check-in, one check-out for the whole party; VisitorLogMember rows are descriptive only.
public class VisitorLog : ITenantScoped
{
    public int VisitorLogId { get; set; }
    public int SocietyId { get; set; }
    public int FlatId { get; set; }
    public int VisitorCategoryId { get; set; }
    public string PrimaryVisitorName { get; set; } = string.Empty;
    public string? PrimaryMobile { get; set; }
    public string? VehicleNo { get; set; }
    public string? CompanyName { get; set; }
    public int PartySize { get; set; } = 1;
    public string? Purpose { get; set; }

    /// Copied from VisitorCategory.RequiresApprovalDefault at creation time — no per-visit guard
    /// override (resolved decision #4).
    public bool ApprovalRequired { get; set; }
    public string Status { get; set; } = VisitorLogStatuses.PendingApproval;
    public DateTime? EntryTime { get; set; }
    public DateTime? ExitTime { get; set; }
    public int? LoggedByUserId { get; set; }
    public int? CheckedOutByUserId { get; set; }
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
}
