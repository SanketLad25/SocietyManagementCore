using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

/// Admin-triggered operations for the chatbot's ingestion/embedding pipeline (Phase 2 of
/// SocChatBot.md). No UI this phase — a plain, unglamorous "run it" endpoint, same as the plan asks
/// for EmbeddingReindexService. Both actions are NoticeManagerRoles-gated, same as KnowledgeArticle
/// writes, since both operate on the same knowledge-content surface. Both are bulk data mutations
/// (delete/recreate every chunk+embedding for a society) so both audit-log, matching this app's
/// "admin-initiated state changes are traceable" convention (SocChatBot.md Fix 4).
[ApiController]
[Route("api/chatbot-admin")]
[Authorize(Roles = RoleNames.NoticeManagerRoles)]
public class ChatbotAdminController(
    IngestionService ingestionService,
    EmbeddingReindexService reindexService,
    ICurrentUserContext currentUser,
    AuditLogService auditLog)
    : ControllerBase
{
    [HttpPost("ingest")]
    public async Task<ActionResult> IngestAllPublished()
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return BadRequest(new { message = "No society context for this account." });
        }

        var result = await ingestionService.IngestAllPublishedAsync(societyId);
        if (!result.Success)
        {
            return Conflict(new { message = result.Error });
        }

        await auditLog.LogAsync("Ingest", "DocumentChunk", null, $"Ingested {result.Data} chunk(s) for society {societyId}.");
        return Ok(new { chunkCount = result.Data });
    }

    [HttpPost("reembed-outdated")]
    public async Task<ActionResult> ReembedOutdated()
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return BadRequest(new { message = "No society context for this account." });
        }

        var result = await reindexService.ReembedOutdatedAsync(societyId);
        if (!result.Success)
        {
            return Conflict(new { message = result.Error });
        }

        await auditLog.LogAsync("Reembed", "ChunkEmbedding", null, $"Re-embedded {result.Data} row(s) for society {societyId}.");
        return Ok(new { reembeddedCount = result.Data });
    }
}
