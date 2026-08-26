namespace SocietyManagementCore.Models.Entities;

/// Plain entity — no ITenantScoped/IAuditable, same documented exception as ChatSession above: one
/// insert-only row per turn (user or assistant) in a ChatSession's history, never edited.
///
/// Named ChatMessageRecord rather than ChatMessage (which would otherwise match the ChatMessage
/// table exactly, as every other entity in this app matches its table name) because
/// Services.ChatMessage already exists — the provider-neutral "one turn of an LLM conversation" DTO
/// introduced in Phase 3 (SocietyManagementCore.Services / IChatCompletionService.cs). Both
/// namespaces are imported unqualified in several chat-related files (SocietyDbContext.cs,
/// ChatOrchestrationService.cs), so keeping both classes literally named ChatMessage would be a
/// compile-time ambiguity, not just a readability problem. Mapped onto the real ChatMessage table
/// via .ToTable("ChatMessage") in SocietyDbContext — the deliberate, documented exception to this
/// codebase's "entity name matches table name" convention.
///
/// Content stores only the plain display text for the turn — for a "user" row, the resident's own
/// message; for an "assistant" row, the model's final answer after its tool-use loop concluded, not
/// the raw tool_use/tool_result scaffolding. That scaffolding is never persisted or replayed —
/// ChatOrchestrationService reconstructs prior turns as plain text messages for the next turn's
/// history, re-running retrieval/tools fresh each time.
///
/// Citations (JSON-encoded list of {SourceType, SourceId, Title}, see
/// ChatOrchestrationService.Citation) is populated only on "assistant" rows whose answer cited
/// retrieved Notice/KnowledgeArticle content — added by 013_chat_message_citations.sql, appended
/// after 012_chatbot.sql rather than editing that already-committed migration in place.
public class ChatMessageRecord
{
    public int ChatMessageId { get; set; }
    public int SocietyId { get; set; }
    public int ChatSessionId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Citations { get; set; }
    public DateTime CreatedOn { get; set; }
}

public static class ChatMessageRoles
{
    public const string User = "user";
    public const string Assistant = "assistant";
}
