namespace SocietyManagementCore.Models.Entities;

/// Plain entity — no ITenantScoped/IAuditable, same documented exception as DocumentChunk above.
/// EmbeddingProvider/EmbeddingModel are separate columns (not one combined string) so
/// EmbeddingReindexService can select "every row not produced by the currently configured
/// provider+model" with a plain WHERE clause (SocChatBot.md "New schema").
///
/// Embedding is mapped as float[] with HasColumnType("vector(1024)") in SocietyDbContext — see the
/// Fluent API mapping for the EFCore.SqlServer.VectorSearch package decision.
public class ChunkEmbedding
{
    public int ChunkEmbeddingId { get; set; }
    public int DocumentChunkId { get; set; }
    public int SocietyId { get; set; }
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public string EmbeddingProvider { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
}
