using System.ComponentModel.DataAnnotations;

namespace SocietyManagementCore.Models.Dtos;

/// SocChatBot.md Phase 4: ChatController's POST /api/chat/message body. Deliberately just the
/// message text — no residentId/societyId/sessionId accepted from the client, per this app's
/// established anti-injection rule (both are resolved server-side only, from
/// ICurrentUserContext/the caller's existing ChatSession).
public class ChatSendMessageRequest
{
    [Required, MinLength(1), MaxLength(4000)]
    public string Message { get; set; } = string.Empty;
}

/// One resolved citation for a RAG-sourced answer — SourceType/SourceId identify the retrieved
/// DocumentChunk's source (see DocumentChunkSourceTypes), Title is the human-readable label
/// ChatOrchestrationService resolves by looking the source up in NoticeService/KnowledgeArticleService
/// (RetrievedChunk itself carries no title).
public record ChatCitationResponse(string SourceType, int SourceId, string Title);

/// One row of a ChatSession's transcript, for ChatController's GET /api/chat/history (ChatWidget.jsx
/// restoring a session on mount — no conversation-history sidebar is in scope, this is just replaying
/// the caller's own single continuous session).
public class ChatHistoryEntryResponse
{
    public required string Role { get; init; }
    public required string Content { get; init; }
    public required DateTime CreatedOn { get; init; }
    public IReadOnlyList<ChatCitationResponse>? Citations { get; init; }
}

/// SocChatBot.md "Phase 5 design decisions": the same {Token, ToolName, Summary} shape whether it
/// arrives live via the "pending_action" SSE event during an active turn, or is surfaced by
/// GET /api/chat/history on widget reopen (a still-Pending, non-expired ChatPendingAction row) — one
/// shape, two delivery paths, so ChatWidget.jsx renders the Confirm/Cancel UI identically either way.
public record ChatPendingActionResponse(string Token, string ToolName, string Summary);

/// GET /api/chat/history's response envelope. Widened from a bare array (Phase 4) to also carry a
/// still-open pending action, if any — a bare array has nowhere to put that without inventing a
/// synthetic "message" row for something that was never persisted as a ChatMessageRecord.
public class ChatHistoryResponse
{
    public required List<ChatHistoryEntryResponse> Messages { get; init; }
    public ChatPendingActionResponse? PendingAction { get; init; }
}

/// POST /api/chat/confirm's request body. Token is the opaque identifier ChatOrchestrationService
/// handed back in the "pending_action" event/history response (in practice, the same server-generated
/// GUID also stored as the eventual Complaint.IdempotencyKey — see ChatPendingAction.cs) — never a
/// residentId/societyId/tool name, per this app's established anti-injection rule.
public class ChatConfirmActionRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;

    public bool Approved { get; set; }
}
