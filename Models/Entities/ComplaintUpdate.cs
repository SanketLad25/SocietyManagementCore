namespace SocietyManagementCore.Models.Entities;

/// Plain class — not ITenantScoped/IAuditable. One insert-only row per assignment, status change,
/// or comment (complaints.md §2.3) — this is the timeline every viewer sees, so nothing here is
/// ever edited or removed.
public class ComplaintUpdate
{
    public int ComplaintUpdateId { get; set; }
    public int SocietyId { get; set; }
    public int ComplaintId { get; set; }
    public string UpdateType { get; set; } = string.Empty;
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }
    public string? CommentText { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime CreatedOn { get; set; }
}
