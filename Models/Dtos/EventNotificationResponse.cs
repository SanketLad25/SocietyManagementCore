namespace SocietyManagementCore.Models.Dtos;

public class EventNotificationResponse
{
    public int EventNotificationId { get; set; }
    public int EventId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedOn { get; set; }
}
