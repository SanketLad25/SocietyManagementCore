using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;

namespace SocietyManagementCore.Services;

/// Re-embedding path, built now even though it's not needed until a provider switch actually
/// happens (SocChatBot.md "Embedding provider abstraction"). Updates rows in place (same
/// ChunkEmbeddingId, new Embedding/EmbeddingProvider/EmbeddingModel) rather than delete-and-reinsert,
/// so DocumentChunk FKs and any future audit trail stay stable.
public class EmbeddingReindexService(SocietyDbContext db, IEmbeddingService embeddingService, ILogger<EmbeddingReindexService> logger)
{
    public async Task<ServiceResult<int>> ReembedOutdatedAsync(int societyId, CancellationToken ct = default)
    {
        var outdated = await db.ChunkEmbeddings
            .Where(e => e.SocietyId == societyId
                && (e.EmbeddingProvider != embeddingService.ProviderName || e.EmbeddingModel != embeddingService.ModelName))
            .ToListAsync(ct);

        if (outdated.Count == 0)
        {
            return ServiceResult<int>.Ok(0);
        }

        var chunkIds = outdated.Select(e => e.DocumentChunkId).Distinct().ToList();
        var chunkTextById = await db.DocumentChunks
            .Where(c => chunkIds.Contains(c.DocumentChunkId))
            .ToDictionaryAsync(c => c.DocumentChunkId, c => c.ChunkText, ct);

        var reembeddedCount = 0;
        foreach (var embeddingRow in outdated)
        {
            if (!chunkTextById.TryGetValue(embeddingRow.DocumentChunkId, out var chunkText))
            {
                // Orphaned ChunkEmbedding row with no backing DocumentChunk — skip rather than throw,
                // this shouldn't happen given IngestionService's delete-both-together cleanup, but
                // isn't this method's job to repair.
                continue;
            }

            float[] newVector;
            try
            {
                newVector = await embeddingService.EmbedAsync(chunkText, EmbeddingKind.Document, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // SocChatBot.md Fix 3: don't let one bad embedding call blow up the whole reindex
                // run or surface the raw provider response to the caller. Whatever was already
                // re-embedded before the failure stays committed below (partial progress is safe —
                // ChunkEmbedding rows are updated in place, so a retried run just picks up where
                // this one stopped).
                logger.LogError(ex,
                    "Embedding failed while re-embedding ChunkEmbedding {ChunkEmbeddingId} (DocumentChunk {DocumentChunkId}) for society {SocietyId}.",
                    embeddingRow.ChunkEmbeddingId, embeddingRow.DocumentChunkId, societyId);

                await db.SaveChangesAsync(ct);
                return ServiceResult<int>.Fail(
                    $"Could not generate embeddings for this content right now ({reembeddedCount} of {outdated.Count} row(s) re-embedded before the failure). Please try again shortly.");
            }

            embeddingRow.Embedding = newVector;
            embeddingRow.EmbeddingProvider = embeddingService.ProviderName;
            embeddingRow.EmbeddingModel = embeddingService.ModelName;
            reembeddedCount++;
        }

        await db.SaveChangesAsync(ct);

        return ServiceResult<int>.Ok(reembeddedCount);
    }
}
