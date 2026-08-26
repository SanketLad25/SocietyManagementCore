namespace SocietyManagementCore.Models.Entities;

/// Plain class — per-recipient notification row, same shape as ComplaintNotification. One row per
/// (visitor log, recipient, event). NotifyFlatResidentsAsync inserts one row per resident on the
/// flat (multi-recipient loop, same idiom as ComplaintNotificationService.NotifyManagersAsync).
public class VisitorNotification
{
    public int VisitorNotificationId { get; set; }
    public int SocietyId { get; set; }
    public int VisitorLogId { get; set; }
    public int RecipientUserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedOn { get; set; }
}
