using System.Text.Json;

namespace SocietyManagementCore.Services;

/// SocChatBot.md "LLM provider abstraction" — the only thing the tool-use loop orchestration
/// (ChatToolLoopRunner) and ChatToolCatalog are allowed to depend on for "what should the model do
/// next." Mirrors IEmbeddingService's role from Phase 2 exactly: ClaudeClient (the real, paid
/// Anthropic Messages API) and StubChatCompletionService (a free, deterministic fake) are
/// interchangeable behind this interface via a Program.cs config switch, so development can proceed
/// with no API credit and no code change is needed later to point at the real provider.
///
/// Deliberately a neutral shape, not a 1:1 mirror of Anthropic's wire format — ClaudeClient
/// translates to/from ChatCompletionRequest/ChatCompletionResult; nothing outside ClaudeClient.cs
/// itself should ever reference Anthropic-specific types (ClaudeMessageRequestBody,
/// ClaudeConversationMessage, the wire-shaped ClaudeContentBlock, etc.).
public interface IChatCompletionService
{
    string ProviderName { get; }

    Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken ct = default);

    /// SocChatBot.md Phase 4: promoted onto the interface (Phase 3 had this as a duck-typed method
    /// with an identical signature on both ClaudeClient and StubChatCompletionService, not part of
    /// IChatCompletionService — fine while nothing called it polymorphically, but
    /// ChatToolLoopRunner.RunToolLoopStreamingAsync now needs to invoke whichever provider DI
    /// resolves, which requires actual interface dispatch). Streams a single turn as incremental
    /// text-delta chunks followed by one Done chunk carrying the fully-assembled content blocks
    /// (including any tool_use blocks). Same neutral request shape as CompleteAsync.
    IAsyncEnumerable<ChatStreamChunk> StreamAsync(ChatCompletionRequest request, CancellationToken ct = default);
}

/// Who "said" a given turn. Anthropic (and most Messages-API-shaped providers) model tool_result
/// content as part of a "user" turn — that convention is preserved here rather than inventing a
/// third role, since it's what lets ChatToolLoopRunner append tool results without a provider-specific
/// translation step.
public enum ChatRole
{
    User,
    Assistant,
}

public enum ChatContentBlockType
{
    Text,
    ToolUse,
    ToolResult,
}

/// One entry of a message's content array — text, a model-issued tool call, or a caller-supplied
/// tool result. A single class with nullable/per-kind fields (rather than a discriminated
/// hierarchy) mirrors how loosely-typed every real provider's wire shape for this actually is, and
/// keeps ChatToolLoopRunner's pattern-matching simple.
public class ChatContentBlock
{
    public required ChatContentBlockType Type { get; init; }

    // Text
    public string? Text { get; init; }

    // ToolUse (assistant/model asking to call a tool)
    public string? ToolUseId { get; init; }
    public string? ToolName { get; init; }
    public JsonElement? ToolInput { get; init; }

    // ToolResult (caller replying with a tool's output; sent back as part of a User-role message)
    public string? ToolResultForId { get; init; }
    public string? ToolResultContent { get; init; }
    public bool ToolResultIsError { get; init; }

    public static ChatContentBlock TextBlock(string text) => new() { Type = ChatContentBlockType.Text, Text = text };

    public static ChatContentBlock ToolUse(string id, string name, JsonElement input) =>
        new() { Type = ChatContentBlockType.ToolUse, ToolUseId = id, ToolName = name, ToolInput = input };

    public static ChatContentBlock ToolResult(string toolUseId, string content, bool isError) =>
        new() { Type = ChatContentBlockType.ToolResult, ToolResultForId = toolUseId, ToolResultContent = content, ToolResultIsError = isError };
}

public class ChatMessage
{
    public required ChatRole Role { get; init; }
    public required List<ChatContentBlock> Content { get; init; }

    public static ChatMessage FromUserText(string text) =>
        new() { Role = ChatRole.User, Content = new List<ChatContentBlock> { ChatContentBlock.TextBlock(text) } };

    public static ChatMessage FromAssistant(List<ChatContentBlock> content) =>
        new() { Role = ChatRole.Assistant, Content = content };

    /// Phase 4 convenience: ChatOrchestrationService replays a ChatSession's prior turns as plain
    /// text (see ChatMessageRecord's doc comment — tool_use/tool_result scaffolding is never
    /// persisted or replayed), so an assistant turn from history is always just one text block.
    public static ChatMessage FromAssistantText(string text) =>
        new() { Role = ChatRole.Assistant, Content = new List<ChatContentBlock> { ChatContentBlock.TextBlock(text) } };

    public static ChatMessage ToolResults(List<ChatContentBlock> toolResultBlocks) =>
        new() { Role = ChatRole.User, Content = toolResultBlocks };
}

/// A provider-neutral tool schema. InputSchema is a plain JSON-Schema-shaped object — that's already
/// provider-neutral (every Messages-API-shaped tool-calling provider uses JSON Schema for arguments),
/// so no translation is needed for this field specifically. Per SocChatBot.md's "Tool input_schema
/// rule", no tool built in this app may declare residentId/societyId/flatId as a schema property.
public class ChatToolDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public object InputSchema { get; init; } = new { type = "object", properties = new { } };
}

public class ChatCompletionRequest
{
    public required string SystemPrompt { get; init; }
    public required List<ChatMessage> Messages { get; init; }
    public IReadOnlyList<ChatToolDefinition> Tools { get; init; } = Array.Empty<ChatToolDefinition>();
    public int MaxTokens { get; init; } = 1024;
}

/// Neutral terminal states every provider's turn can end in. `Other` covers anything a provider
/// returns that this app doesn't specifically special-case (e.g. Anthropic's "pause_turn," or a
/// future provider's own vocabulary) — RawStopReason on ChatCompletionResult preserves the original
/// string for logging even when it maps to Other.
public enum ChatStopReason
{
    EndTurn,
    ToolUse,
    MaxTokens,
    StopSequence,
    Other,
}

public class ChatCompletionResult
{
    public required ChatStopReason StopReason { get; init; }

    /// The provider's own stop-reason string (e.g. "end_turn", "tool_use", "max_tokens") — kept
    /// alongside the neutral enum purely for logging/diagnostics, never branched on outside the
    /// provider implementation that produced it.
    public string? RawStopReason { get; init; }

    public required List<ChatContentBlock> Content { get; init; }
}

/// One chunk of IChatCompletionService.StreamAsync's output — either an incremental text delta to
/// render immediately, or the final "done" chunk carrying the fully-assembled content blocks
/// (including any tool_use blocks) once the turn finishes. FinalContent is the neutral
/// ChatContentBlock type, never a provider-specific wire type — ClaudeClient's implementation
/// translates from Anthropic's SSE shape before yielding, same as CompleteAsync's translation.
public class ChatStreamChunk
{
    public bool IsTextDelta { get; init; }
    public string? TextDelta { get; init; }
    public bool IsDone { get; init; }
    public ChatStopReason StopReason { get; init; }
    public string? RawStopReason { get; init; }
    public List<ChatContentBlock>? FinalContent { get; init; }

    public static ChatStreamChunk TextDeltaChunk(string text) => new() { IsTextDelta = true, TextDelta = text };

    public static ChatStreamChunk DoneChunk(ChatStopReason stopReason, string? rawStopReason, List<ChatContentBlock> content) =>
        new() { IsDone = true, StopReason = stopReason, RawStopReason = rawStopReason, FinalContent = content };
}
