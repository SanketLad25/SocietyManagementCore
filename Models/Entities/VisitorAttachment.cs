namespace SocietyManagementCore.Models.Entities;

/// Plain class — not ITenantScoped/IAuditable, same documented exception as ComplaintAttachment
/// (immutable once uploaded: delete + re-upload, never edited in place).
public class VisitorAttachment
{
    public int VisitorAttachmentId { get; set; }
    public int SocietyId { get; set; }
    public int VisitorLogId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public DateTime UploadedOn { get; set; }
    public int? UploadedBy { get; set; }
}
