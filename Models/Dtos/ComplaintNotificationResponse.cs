namespace SocietyManagementCore.Models.Dtos;

public class ComplaintNotificationResponse
{
    public int ComplaintNotificationId { get; set; }
    public int ComplaintId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedOn { get; set; }
}
