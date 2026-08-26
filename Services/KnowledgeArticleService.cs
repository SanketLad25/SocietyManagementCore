using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

public class KnowledgeArticleService(
    SocietyDbContext db,
    ICurrentUserContext currentUser,
    AuditLogService auditLog,
    IngestionService ingestionService,
    ILogger<KnowledgeArticleService> logger)
{
    private static readonly string[] ManagerRoles = RoleNames.NoticeManagerRoles.Split(',');

    public async Task<List<KnowledgeArticleResponse>> ListAsync()
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return new List<KnowledgeArticleResponse>();
        }

        var query = db.KnowledgeArticles.Where(a => a.SocietyId == societyId);
        if (!IsManager())
        {
            query = query.Where(a => a.IsPublished);
        }

        var articles = await query
            .OrderBy(a => a.Category)
            .ThenBy(a => a.Title)
            .ToListAsync();

        return articles.Select(ToResponse).ToList();
    }

    private bool IsManager() => currentUser.RoleName is { } role && ManagerRoles.Contains(role);

    /// SocChatBot.md Phase 4: ChatOrchestrationService's citation-title lookup for a retrieved
    /// DocumentChunk whose SourceType is KnowledgeArticle — mirrors NoticeService.GetAsync's role in
    /// the same lookup for SourceType Notice. Deliberately returns null for an unpublished article
    /// regardless of caller role (unlike NoticeService.GetAsync, which lets managers see drafts) —
    /// an unpublished article's chunks should already be gone via the delete-on-unpublish path
    /// (Phase 2), so this only matters for the rare race the plan calls out ("should be rare given
    /// delete-on-unpublish, but don't crash if it happens"); the caller (ChatOrchestrationService)
    /// treats a null return as "omit this citation," never as an error.
    public async Task<KnowledgeArticleResponse?> GetPublishedAsync(int articleId)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return null;
        }

        var article = await db.KnowledgeArticles
            .FirstOrDefaultAsync(a => a.KnowledgeArticleId == articleId && a.SocietyId == societyId && a.IsPublished);

        return article is null ? null : ToResponse(article);
    }

    public async Task<ServiceResult<KnowledgeArticleResponse>> CreateAsync(KnowledgeArticleRequest request)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<KnowledgeArticleResponse>.Fail("No society context for this account.");
        }

        var article = new KnowledgeArticle
        {
            SocietyId = societyId,
            Title = request.Title,
            Body = request.Body,
            Category = request.Category,
            IsPublished = request.IsPublished,
        };

        db.KnowledgeArticles.Add(article);
        await db.SaveChangesAsync();
        await auditLog.LogAsync("Create", "KnowledgeArticle", article.KnowledgeArticleId, article.Title);

        // SocChatBot.md Fix 1: a newly-published article must be searchable immediately, not only
        // after the next admin-triggered IngestAllPublishedAsync run. An ingestion failure here must
        // not roll back the article save that already succeeded — log it and move on.
        if (article.IsPublished)
        {
            await IngestAndLogFailureAsync(societyId, article);
        }

        return ServiceResult<KnowledgeArticleResponse>.Ok(ToResponse(article));
    }

    public async Task<ServiceResult<KnowledgeArticleResponse>> UpdateAsync(int articleId, KnowledgeArticleRequest request)
    {
        var article = await FindInSocietyAsync(articleId);
        if (article is null)
        {
            return ServiceResult<KnowledgeArticleResponse>.Fail("Article not found.");
        }

        var wasPublished = article.IsPublished;

        article.Title = request.Title;
        article.Body = request.Body;
        article.Category = request.Category;
        article.IsPublished = request.IsPublished;

        await db.SaveChangesAsync();
        await auditLog.LogAsync("Update", "KnowledgeArticle", article.KnowledgeArticleId, article.Title);

        // SocChatBot.md Fix 1 (the blocker): keep DocumentChunk/ChunkEmbedding in sync with this
        // save's publish state, immediately, rather than leaving stale chunks live until the next
        // manual re-ingest —
        //   published (still or newly) -> re-ingest (upsert; IngestSourceAsync deletes-then-recreates,
        //     so an edit to already-published content is picked up too)
        //   was published, now unpublished -> delete the existing chunks, don't recreate empty ones
        //   was unpublished, still unpublished -> nothing to do, no chunks exist
        if (article.IsPublished)
        {
            await IngestAndLogFailureAsync(article.SocietyId, article);
        }
        else if (wasPublished)
        {
            // A failure here (e.g. a transient DB blip) must not roll back the article update that
            // already succeeded — log it and move on, same as an ingestion failure above does.
            var removeResult = await ingestionService.RemoveSourceChunksAsync(article.SocietyId, DocumentChunkSourceTypes.KnowledgeArticle, article.KnowledgeArticleId);
            if (!removeResult.Success)
            {
                logger.LogWarning(
                    "Removing search index chunks failed for unpublished KnowledgeArticle {KnowledgeArticleId} in society {SocietyId}: {Error}",
                    article.KnowledgeArticleId, article.SocietyId, removeResult.Error);
            }
        }

        return ServiceResult<KnowledgeArticleResponse>.Ok(ToResponse(article));
    }

    private async Task IngestAndLogFailureAsync(int societyId, KnowledgeArticle article)
    {
        var ingestResult = await ingestionService.IngestKnowledgeArticleAsync(societyId, article);
        if (!ingestResult.Success)
        {
            logger.LogWarning(
                "Ingestion failed for KnowledgeArticle {KnowledgeArticleId} in society {SocietyId}: {Error}",
                article.KnowledgeArticleId, societyId, ingestResult.Error);
        }
    }

    private Task<KnowledgeArticle?> FindInSocietyAsync(int articleId) =>
        currentUser.SocietyId is { } societyId
            ? db.KnowledgeArticles.FirstOrDefaultAsync(a => a.KnowledgeArticleId == articleId && a.SocietyId == societyId)
            : Task.FromResult<KnowledgeArticle?>(null);

    private static KnowledgeArticleResponse ToResponse(KnowledgeArticle article) => new()
    {
        KnowledgeArticleId = article.KnowledgeArticleId,
        Title = article.Title,
        Body = article.Body,
        Category = article.Category,
        IsPublished = article.IsPublished,
        CreatedOn = article.CreatedOn,
        ModifiedOn = article.ModifiedOn,
    };
}
