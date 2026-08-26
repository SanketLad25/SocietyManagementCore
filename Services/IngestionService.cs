using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

/// Chunks published Notice + published KnowledgeArticle content, embeds each chunk via
/// IEmbeddingService (never a concrete provider type — SocChatBot.md "Embedding provider
/// abstraction"), and stores DocumentChunk + ChunkEmbedding rows.
///
/// `societyId` is an explicit parameter throughout, not resolved internally from
/// ICurrentUserContext — same reasoning as IRetrievalService: this needs to run outside a real HTTP
/// request (a future scheduled/triggered re-ingestion job, or a test harness), so callers with an
/// HTTP context (a future admin controller) resolve currentUser.SocietyId and pass it in.
public class IngestionService(SocietyDbContext db, IEmbeddingService embeddingService, ILogger<IngestionService> logger)
{
    // Word-count heuristic (this backend has no tokenizer library): target ~450 words per chunk,
    // comfortably inside the plan's ~300-600 "tokens" guidance, with a 50-word overlap between
    // consecutive chunks so a sentence/idea split across a chunk boundary still appears whole in at
    // least one chunk. Short sources (a single FAQ shorter than the target) become exactly one chunk.
    private const int TargetChunkWords = 450;
    private const int OverlapWords = 50;

    /// Ingests every published Notice and published KnowledgeArticle for `societyId`. Returns the
    /// total number of DocumentChunk rows written on success. Safe to call repeatedly — each
    /// source's existing chunks (and their embeddings) are deleted and recreated, so re-running
    /// never accumulates duplicates.
    ///
    /// Defense-in-depth safety net (SocChatBot.md Fix 1, bullet 3): after (re)ingesting the
    /// currently-published set, deletes any DocumentChunk/ChunkEmbedding for this society whose
    /// (SourceType, SourceId) is no longer in that set — this catches both a source that flipped to
    /// unpublished *and* a source that was hard-deleted entirely (neither appears in the
    /// currently-published lists below), for any case the CreateAsync/UpdateAsync/PublishAsync/
    /// DeleteAsync hooks on KnowledgeArticleService/NoticeService miss (e.g. a hook forgotten on a
    /// future code path). It is a backstop, not a replacement for those hooks, since this method is
    /// only admin-triggered today (no scheduled job calls it).
    public async Task<ServiceResult<int>> IngestAllPublishedAsync(int societyId, CancellationToken ct = default)
    {
        var totalChunks = 0;

        var notices = await db.Notices
            .Where(n => n.SocietyId == societyId && n.Status == NoticeStatuses.Published)
            .ToListAsync(ct);

        foreach (var notice in notices)
        {
            var text = $"{notice.Title}\n\n{notice.Description}";
            var result = await IngestSourceAsync(societyId, DocumentChunkSourceTypes.Notice, notice.NoticeId, text, ct);
            if (!result.Success)
            {
                return ServiceResult<int>.Fail(result.Error!);
            }

            totalChunks += result.Data;
        }

        // Mirrors KnowledgeArticleService.ListAsync's non-manager filter exactly (IsPublished only)
        // — querying KnowledgeArticle directly here without this filter would re-introduce the
        // draft-leak bug that service was already fixed for.
        var articles = await db.KnowledgeArticles
            .Where(a => a.SocietyId == societyId && a.IsPublished)
            .ToListAsync(ct);

        foreach (var article in articles)
        {
            var text = $"{article.Title}\n\n{article.Body}";
            var result = await IngestSourceAsync(societyId, DocumentChunkSourceTypes.KnowledgeArticle, article.KnowledgeArticleId, text, ct);
            if (!result.Success)
            {
                return ServiceResult<int>.Fail(result.Error!);
            }

            totalChunks += result.Data;
        }

        var publishedKeys = notices
            .Select(n => (SourceType: DocumentChunkSourceTypes.Notice, SourceId: n.NoticeId))
            .Concat(articles.Select(a => (SourceType: DocumentChunkSourceTypes.KnowledgeArticle, SourceId: a.KnowledgeArticleId)))
            .ToHashSet();

        var existingSources = await db.DocumentChunks
            .Where(c => c.SocietyId == societyId)
            .Select(c => new { c.SourceType, c.SourceId })
            .Distinct()
            .ToListAsync(ct);

        foreach (var source in existingSources)
        {
            if (!publishedKeys.Contains((source.SourceType, source.SourceId)))
            {
                var deleteResult = await DeleteExistingChunksAsync(societyId, source.SourceType, source.SourceId, ct);
                if (!deleteResult.Success)
                {
                    return ServiceResult<int>.Fail(deleteResult.Error!);
                }
            }
        }

        return ServiceResult<int>.Ok(totalChunks);
    }

    /// Re-chunks and re-embeds a single source, replacing whatever chunks/embeddings already exist
    /// for it. Called directly from KnowledgeArticleService.CreateAsync/UpdateAsync and
    /// NoticeService.PublishAsync when a source becomes (or stays) published, so re-ingestion
    /// happens immediately rather than waiting for the next admin-triggered IngestAllPublishedAsync
    /// run (SocChatBot.md Fix 1).
    public Task<ServiceResult<int>> IngestNoticeAsync(int societyId, Notice notice, CancellationToken ct = default) =>
        IngestSourceAsync(societyId, DocumentChunkSourceTypes.Notice, notice.NoticeId, $"{notice.Title}\n\n{notice.Description}", ct);

    public Task<ServiceResult<int>> IngestKnowledgeArticleAsync(int societyId, KnowledgeArticle article, CancellationToken ct = default) =>
        IngestSourceAsync(societyId, DocumentChunkSourceTypes.KnowledgeArticle, article.KnowledgeArticleId, $"{article.Title}\n\n{article.Body}", ct);

    /// Deletes a source's existing chunks/embeddings without recreating them — the unpublish/delete
    /// half of Fix 1's hook pair. Public wrapper around the same helper IngestSourceAsync uses
    /// internally, so "remove" and "replace" always agree on what "this source's chunks" means.
    /// Returns ServiceResult so a DB failure here (e.g. a transient timeout/deadlock) can be
    /// logged-and-swallowed by the caller the same way an embedding failure already is, rather than
    /// propagating past the Notice/KnowledgeArticle save that already committed successfully.
    public Task<ServiceResult<bool>> RemoveSourceChunksAsync(int societyId, string sourceType, int sourceId, CancellationToken ct = default) =>
        DeleteExistingChunksAsync(societyId, sourceType, sourceId, ct);

    private async Task<ServiceResult<int>> IngestSourceAsync(int societyId, string sourceType, int sourceId, string fullText, CancellationToken ct)
    {
        var deleteResult = await DeleteExistingChunksAsync(societyId, sourceType, sourceId, ct);
        if (!deleteResult.Success)
        {
            return ServiceResult<int>.Fail(deleteResult.Error!);
        }

        var chunkTexts = ChunkText(fullText);
        var index = 0;

        foreach (var chunkText in chunkTexts)
        {
            float[] vector;
            try
            {
                vector = await embeddingService.EmbedAsync(chunkText, EmbeddingKind.Document, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // SocChatBot.md Fix 3: an embedding-API failure (Voyage HTTP failure or parse
                // failure — any of VoyageEmbeddingService's defensive exception types) must not
                // propagate as an unhandled exception/500. The raw exception (which may include the
                // raw Voyage response body) is logged server-side for diagnostics only; the
                // caller-facing message stays generic.
                logger.LogError(ex,
                    "Embedding failed while ingesting {SourceType} {SourceId} for society {SocietyId} (chunk {ChunkIndex} of {ChunkCount}).",
                    sourceType, sourceId, societyId, index, chunkTexts.Count);

                return ServiceResult<int>.Fail(
                    $"Could not generate embeddings for this content right now ({index} of {chunkTexts.Count} chunk(s) ingested before the failure). Please try ingesting again shortly.");
            }

            try
            {
                var chunk = new DocumentChunk
                {
                    SocietyId = societyId,
                    SourceType = sourceType,
                    SourceId = sourceId,
                    ChunkText = chunkText,
                    ChunkIndex = index,
                    CreatedOn = DateTime.UtcNow,
                };
                db.DocumentChunks.Add(chunk);
                await db.SaveChangesAsync(ct); // need the generated DocumentChunkId before writing the embedding row
                // Optional/low-priority gap (SocChatBot.md, not fixed): a crash between this SaveChangesAsync
                // and the one below would leave this DocumentChunk with no ChunkEmbedding — no navigation
                // property links them and each write is its own SaveChangesAsync. The EmbedAsync call already
                // happens before this point (so a Voyage failure per se can't cause it, per the catch above),
                // but a process kill / DB hiccup between these two lines still could.

                db.ChunkEmbeddings.Add(new ChunkEmbedding
                {
                    DocumentChunkId = chunk.DocumentChunkId,
                    SocietyId = societyId,
                    Embedding = vector,
                    EmbeddingProvider = embeddingService.ProviderName,
                    EmbeddingModel = embeddingService.ModelName,
                    CreatedOn = DateTime.UtcNow,
                });
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Same reasoning as the embedding-failure catch above, but for the DB write itself:
                // a transient DB failure (timeout/deadlock/connection blip) here must not surface as
                // an unhandled exception/500 either — the caller's underlying CRUD save already
                // committed before ingestion ran, so this failure is logged and reported back as a
                // friendly ServiceResult.Fail, not thrown.
                logger.LogError(ex,
                    "Saving chunk/embedding failed while ingesting {SourceType} {SourceId} for society {SocietyId} (chunk {ChunkIndex} of {ChunkCount}).",
                    sourceType, sourceId, societyId, index, chunkTexts.Count);

                return ServiceResult<int>.Fail(
                    $"Could not save this content to the search index right now ({index} of {chunkTexts.Count} chunk(s) ingested before the failure). Please try ingesting again shortly.");
            }

            index++;
        }

        return ServiceResult<int>.Ok(chunkTexts.Count);
    }

    private async Task<ServiceResult<bool>> DeleteExistingChunksAsync(int societyId, string sourceType, int sourceId, CancellationToken ct)
    {
        try
        {
            var existingChunkIds = await db.DocumentChunks
                .Where(c => c.SocietyId == societyId && c.SourceType == sourceType && c.SourceId == sourceId)
                .Select(c => c.DocumentChunkId)
                .ToListAsync(ct);

            if (existingChunkIds.Count == 0)
            {
                return ServiceResult<bool>.Ok(true);
            }

            var existingEmbeddings = db.ChunkEmbeddings.Where(e => existingChunkIds.Contains(e.DocumentChunkId));
            db.ChunkEmbeddings.RemoveRange(existingEmbeddings);
            await db.SaveChangesAsync(ct);

            var existingChunks = db.DocumentChunks.Where(c => existingChunkIds.Contains(c.DocumentChunkId));
            db.DocumentChunks.RemoveRange(existingChunks);
            await db.SaveChangesAsync(ct);

            return ServiceResult<bool>.Ok(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Same reasoning as IngestSourceAsync's write-path catch: a transient DB failure while
            // deleting existing chunks/embeddings must not propagate past the caller's already-
            // committed CRUD save (e.g. NoticeService.DeleteAsync, KnowledgeArticleService.UpdateAsync)
            // as an unhandled exception/500.
            logger.LogError(ex,
                "Deleting existing chunks failed for {SourceType} {SourceId} in society {SocietyId}.",
                sourceType, sourceId, societyId);

            return ServiceResult<bool>.Fail(
                "Could not remove existing search index entries for this content right now. Please try again shortly.");
        }
    }

    /// Splits `text` on whitespace into ~TargetChunkWords-word chunks with OverlapWords of overlap
    /// between consecutive chunks. Text shorter than the target becomes a single chunk.
    internal static List<string> ChunkText(string text)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return new List<string>();
        }

        if (words.Length <= TargetChunkWords)
        {
            return new List<string> { string.Join(' ', words) };
        }

        var chunks = new List<string>();
        var start = 0;
        var step = TargetChunkWords - OverlapWords;

        while (start < words.Length)
        {
            var length = Math.Min(TargetChunkWords, words.Length - start);
            chunks.Add(string.Join(' ', words.Skip(start).Take(length)));

            if (start + length >= words.Length)
            {
                break;
            }

            start += step;
        }

        return chunks;
    }
}
