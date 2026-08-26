namespace SocietyManagementCore.Models.Dtos;

/// Shape of one row from IRetrievalService.SearchAsync's VECTOR_DISTANCE-ordered similarity search.
/// Not a typical controller-facing Response DTO — it's the LINQ projection target for the
/// `select new RetrievedChunk { ... }` in RetrievalService.SearchAsync, so property names must match
/// that projection's member names exactly.
public class RetrievedChunk
{
    public int DocumentChunkId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public int SourceId { get; set; }
    public string ChunkText { get; set; } = string.Empty;
    public double Distance { get; set; }
}
