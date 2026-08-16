namespace SocietyManagementCore.Models.Entities;

/// Plain class — per-recipient notification row (complaints.md §2.5), not a broadcast list like
/// NoticeReadStatus. One row per (complaint, recipient, event).
public class ComplaintNotification
{
    public int ComplaintNotificationId { get; set; }
    public int SocietyId { get; set; }
    public int ComplaintId { get; set; }
    public int RecipientUserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedOn { get; set; }
}
