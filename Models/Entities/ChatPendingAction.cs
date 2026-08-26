namespace SocietyManagementCore.Models.Entities;

/// SocChatBot.md Phase 5: one row per mutating tool_use (raise_complaint/join_event) the model
/// proposed but that ChatToolLoopRunner paused rather than auto-executed, awaiting the resident's
/// explicit Confirm/Cancel. Plain entity — no ITenantScoped/IAuditable, same documented exception as
/// ChatSession/ChatMessageRecord (014_chat_confirmation.sql's doc comment): SocietyId/ChatSessionId
/// are set by hand by ChatOrchestrationService at creation time, and this table has no
/// CreatedBy/ModifiedBy/ModifiedOn columns even though Status is updated once in place.
///
/// IdempotencyKey does double duty: it's both the opaque "token" ChatConfirmActionRequest.Token
/// looks this row up by, and — only for a confirmed raise_complaint — the same value stamped onto
/// Complaint.IdempotencyKey so a retried confirm can never insert a duplicate complaint. Harmless and
/// unused for join_event, which is already upsert-safe via the DB-level
/// UX_EventRegistration_Event_Resident constraint.
///
/// ConversationSnapshotJson is the paused List&lt;Services.ChatMessage&gt; (the provider-neutral
/// LLM-turn DTO, JSON-serialized) at the exact point ChatToolLoopRunner.RunToolLoopStreamingAsync
/// yielded its pending-action event — i.e. including the assistant's tool_use turn, but before any
/// tool_result has been appended. ChatOrchestrationService.ConfirmActionAsync deserializes this,
/// appends the real (or declined) tool_result(s), and resumes the loop with a fresh iteration budget.
public class ChatPendingAction
{
    public int ChatPendingActionId { get; set; }
    public int SocietyId { get; set; }
    public int ChatSessionId { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string ToolInputJson { get; set; } = string.Empty;
    public string ToolUseId { get; set; } = string.Empty;
    public string ConversationSnapshotJson { get; set; } = string.Empty;
    public Guid IdempotencyKey { get; set; }
    public string Status { get; set; } = ChatPendingActionStatuses.Pending;
    public DateTime CreatedOn { get; set; }
    public DateTime ExpiresOn { get; set; }
}

public static class ChatPendingActionStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
    public const string Expired = "Expired";
}
