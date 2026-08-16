namespace SocietyManagementCore.Models.Dtos;

public class ComplaintAttachmentResponse
{
    public int ComplaintAttachmentId { get; set; }
    public string AttachmentKind { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedOn { get; set; }
}
