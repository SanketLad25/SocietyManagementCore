namespace SocietyManagementCore.Models.Entities;

/// Plain entity — no ITenantScoped/IAuditable, same documented exception as NoticeAttachment/
/// ComplaintUpdate (SocChatBot.md "Audit-column shape, resolved explicitly"): insert-only,
/// SocietyId set by hand by IngestionService at creation time.
///
/// SourceType/SourceId deliberately has no FK — a polymorphic pointer at either Notice or
/// KnowledgeArticle, two different tables a single FK constraint can't express.
public class DocumentChunk
{
    public int DocumentChunkId { get; set; }
    public int SocietyId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public int SourceId { get; set; }
    public string ChunkText { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public DateTime CreatedOn { get; set; }
}

public static class DocumentChunkSourceTypes
{
    public const string Notice = "Notice";
    public const string KnowledgeArticle = "KnowledgeArticle";
}
