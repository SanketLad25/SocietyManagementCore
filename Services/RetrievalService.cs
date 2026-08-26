using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;

namespace SocietyManagementCore.Services;

/// SocChatBot.md Fix 2 investigated whether the originally-planned raw-SQL VECTOR_DISTANCE query
/// could instead be plain LINQ, since EFCore.SqlServer.VectorSearch 9.0.0 documents
/// EF.Functions.VectorDistance(...) as usable inside a normal LINQ query. Confirmed directly against
/// this app's real localhost\SQL2025 dev DB on 2026-08-18: this exact join+dual-`.Where()`+orderby+
/// Take shape translates and executes correctly, returning the same DocumentChunkId set as the
/// original raw-SQL version for the same input. Replaced the raw SQL with this LINQ query — this is
/// no longer this codebase's "first raw-SQL query"; the SocietyId filter here is the same
/// compiler-checked `.Where(x => x.SocietyId == societyId)` idiom every other service in this app
/// uses, applied to both sides of the join (ChunkEmbedding and DocumentChunk each carry their own
/// SocietyId — see the "Audit-column shape" section of SocChatBot.md for why both tables have it).
public class RetrievalService(SocietyDbContext db, IEmbeddingService embeddingService) : IRetrievalService
{
    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(int societyId, string query, int topK, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || topK <= 0)
        {
            return Array.Empty<RetrievedChunk>();
        }

        var queryVector = await embeddingService.EmbedAsync(query, EmbeddingKind.Query, ct);

        var results = await (
            from ce in db.ChunkEmbeddings
            join dc in db.DocumentChunks on ce.DocumentChunkId equals dc.DocumentChunkId
            where ce.SocietyId == societyId && dc.SocietyId == societyId
            orderby EF.Functions.VectorDistance("cosine", ce.Embedding, queryVector)
            select new RetrievedChunk
            {
                DocumentChunkId = dc.DocumentChunkId,
                SourceType = dc.SourceType,
                SourceId = dc.SourceId,
                ChunkText = dc.ChunkText,
                Distance = EF.Functions.VectorDistance("cosine", ce.Embedding, queryVector),
            })
            .Take(topK)
            .ToListAsync(ct);

        return results;
    }
}
