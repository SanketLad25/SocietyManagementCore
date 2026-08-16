namespace SocietyManagementCore.Models.Entities;

/// Plain class — implements neither ITenantScoped nor IAuditable (notice.md §2.2, same documented
/// exception as MaintenanceBillLineItem). An attachment is immutable once uploaded: delete +
/// re-upload, never edited in place, so there's no ModifiedOn/ModifiedBy to stamp.
public class NoticeAttachment
{
    public int NoticeAttachmentId { get; set; }
    public int SocietyId { get; set; }
    public int NoticeId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public DateTime UploadedOn { get; set; }
    public int? UploadedBy { get; set; }
}
