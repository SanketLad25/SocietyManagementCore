namespace SocietyManagementCore.Models.Entities;

/// Plain class — not ITenantScoped/IAuditable, same documented exception as NoticeAttachment
/// (immutable once uploaded: delete + re-upload, never edited in place).
public class ComplaintAttachment
{
    public int ComplaintAttachmentId { get; set; }
    public int SocietyId { get; set; }
    public int ComplaintId { get; set; }
    public string AttachmentKind { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public DateTime UploadedOn { get; set; }
    public int? UploadedBy { get; set; }
}
