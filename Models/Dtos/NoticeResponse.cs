namespace SocietyManagementCore.Models.Dtos;

public class NoticeResponse
{
    public int NoticeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime PublishDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadOn { get; set; }
    public bool IsExpired { get; set; }
    public bool IsPinned { get; set; }
    public List<NoticeAttachmentResponse> Attachments { get; set; } = new();
}
