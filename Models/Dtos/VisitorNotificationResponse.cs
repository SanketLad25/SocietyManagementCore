namespace SocietyManagementCore.Models.Dtos;

public class VisitorNotificationResponse
{
    public int VisitorNotificationId { get; set; }
    public int VisitorLogId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedOn { get; set; }
}
