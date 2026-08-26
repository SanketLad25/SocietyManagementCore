namespace SocietyManagementCore.Models.Entities;

public class Complaint : ITenantScoped
{
    public int ComplaintId { get; set; }
    public int SocietyId { get; set; }
    public int ResidentId { get; set; }
    public int? FlatId { get; set; }
    public int CategoryId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = ComplaintPriorities.Medium;
    public string Status { get; set; } = ComplaintStatuses.Open;
    public string? AssignedToName { get; set; }
    public string? AssignedToContact { get; set; }
    public string? AssignmentNotes { get; set; }
    public int? AssignedBy { get; set; }
    public DateTime? AssignedOn { get; set; }
    public bool ResidentConfirmed { get; set; }
    public DateTime? ResidentConfirmedOn { get; set; }
    public DateTime? ClosedOn { get; set; }
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }

    /// SocChatBot.md Phase 5 / 014_chat_confirmation.sql: NULL for every complaint created through
    /// the ordinary REST endpoint. Stamped only when raise_complaint is executed via a confirmed chat
    /// action, so a retried POST /api/chat/confirm with the same token can detect and return the
    /// already-created complaint instead of inserting a duplicate.
    public Guid? IdempotencyKey { get; set; }
}
