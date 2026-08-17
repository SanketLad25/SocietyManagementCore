namespace SocietyManagementCore.Models.Entities;

public class EventNotification
{
    public int EventNotificationId { get; set; }
    public int SocietyId { get; set; }
    public int EventId { get; set; }
    public int RecipientUserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedOn { get; set; }
}
