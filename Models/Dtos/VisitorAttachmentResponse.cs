namespace SocietyManagementCore.Models.Dtos;

public class VisitorAttachmentResponse
{
    public int VisitorAttachmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedOn { get; set; }
}
