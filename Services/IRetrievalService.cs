using SocietyManagementCore.Models.Dtos;

namespace SocietyManagementCore.Services;

public interface IRetrievalService
{
    /// Embeds `query` (EmbeddingKind.Query) and returns the `topK` nearest DocumentChunk rows for
    /// `societyId` by cosine distance, ascending (closest first). `societyId` is an explicit
    /// parameter, not resolved internally from ICurrentUserContext, matching SocChatBot.md's
    /// signature and letting this be exercised outside an HTTP request (e.g. the cross-society leak
    /// test harness) — callers (ChatController/ChatOrchestrationService in a later phase) are
    /// responsible for passing currentUser.SocietyId, never a client-supplied value.
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(int societyId, string query, int topK, CancellationToken ct = default);
}
