using System.Runtime.CompilerServices;
using System.Text.Json;

namespace SocietyManagementCore.Services;

/// Pairs a tool's neutral JSON-schema definition with the handler that actually executes it. Built
/// by ChatToolCatalog (or, for the deliberately-forced max-iterations/tool-failure verification
/// scenarios, an ad hoc tool in the verification harness) — ChatToolLoopRunner itself has no
/// business-logic knowledge of any tool.
public class ChatTool
{
    public required ChatToolDefinition Definition { get; init; }

    /// Widened to take an optional idempotency key (SocChatBot.md Phase 5) alongside the tool-call
    /// input and cancellation token — meaningful only for raise_complaint's handler (ignored by every
    /// other tool, including join_event, which is already upsert-safe via the DB-level
    /// UX_EventRegistration_Event_Resident constraint). Always null when invoked from the ordinary
    /// tool-use loop (ExecuteToolCallsAsync below) — a RequiresConfirmation tool never reaches that
    /// path automatically, only ChatOrchestrationService.ConfirmActionAsync ever passes a real key,
    /// once, at the point the resident actually approves the action.
    public required Func<JsonElement, Guid?, CancellationToken, Task<string>> ExecuteAsync { get; init; }

    /// SocChatBot.md "Phase 5 design decisions": true only for raise_complaint/join_event. Lives on
    /// this server-side pairing of definition+handler, not on ChatToolDefinition — the model never
    /// sees this flag, since ChatToolDefinition is the only part of a ChatTool serialized onto the
    /// wire.
    public bool RequiresConfirmation { get; init; } = false;
}

public record ChatToolCallTrace(string ToolName, JsonElement? Input, string ResultText, bool IsError);

public record ChatConversationResult(
    string FinalText,
    List<ChatToolCallTrace> ToolCalls,
    string? StopReason,
    int Iterations,
    bool HitIterationLimit);

/// One event out of RunToolLoopStreamingAsync — either an incremental text delta to render
/// immediately, or the final Done event once the whole tool-use loop concludes (end_turn, an
/// unrecognized stop reason, or the MaxToolIterations cap). Deliberately does NOT surface a
/// per-tool-call event: SocChatBot.md's Phase 4 UI asks only for a typing indicator plus the final
/// answer/citations, not live tool-call progress — ChatToolCallTrace inside the Done event's Result
/// still carries the full trace for logging/debugging.
public class ChatStreamEvent
{
    public bool IsTextDelta { get; init; }
    public string? TextDelta { get; init; }
    public bool IsDone { get; init; }
    public ChatConversationResult? Result { get; init; }

    /// SocChatBot.md "Phase 5 design decisions": emitted instead of looping again when the current
    /// iteration's tool_use names a RequiresConfirmation tool. The assistant's turn (including every
    /// tool_use block it contained, confirmation-gated or not) has already been appended to
    /// `conversation` by the time this is yielded — ChatOrchestrationService snapshots that same list
    /// reference to resume later.
    public bool IsPendingAction { get; init; }
    public string? PendingToolName { get; init; }
    public JsonElement? PendingToolInput { get; init; }
    public string? PendingToolUseId { get; init; }

    public static ChatStreamEvent TextDeltaEvent(string text) => new() { IsTextDelta = true, TextDelta = text };
    public static ChatStreamEvent DoneEvent(ChatConversationResult result) => new() { IsDone = true, Result = result };

    public static ChatStreamEvent PendingActionEvent(string toolName, JsonElement? toolInput, string toolUseId) =>
        new() { IsPendingAction = true, PendingToolName = toolName, PendingToolInput = toolInput, PendingToolUseId = toolUseId };
}

/// SocChatBot.md "LLM provider abstraction": the generic tool-use loop mechanics — the explicit
/// termination bound, tool-failure handling, and unrecognized-stop_reason handling — extracted out
/// of ClaudeClient so this class depends only on IChatCompletionService, never on ClaudeClient
/// directly. Same discipline as IngestionService never referencing VoyageEmbeddingService: whichever
/// provider is behind IChatCompletionService (real Anthropic or the free StubChatCompletionService),
/// this loop runs identically.
public class ChatToolLoopRunner(IChatCompletionService chatCompletionService, ILogger<ChatToolLoopRunner> logger)
{
    /// If the model keeps emitting tool_use past this many round trips, the loop stops gracefully
    /// instead of looping forever / running up API cost (SocChatBot.md "ClaudeClient's tool-use loop
    /// needs an explicit termination bound").
    public const int MaxToolIterations = 6;

    /// Sends the user's message, and if the model responds with ToolUse, executes the matching
    /// tool(s) from `tools` and feeds the result(s) back as a tool-results message, repeating until
    /// the model reaches EndTurn/StopSequence, a tool fails, an unrecognized stop reason is
    /// returned, or MaxToolIterations round trips have happened — whichever comes first. Never
    /// throws for a tool failure or an unrecognized stop reason; both terminate the loop gracefully
    /// with an explanatory result instead.
    public Task<ChatConversationResult> RunToolLoopAsync(
        string systemPrompt,
        string userMessage,
        IReadOnlyList<ChatTool> tools,
        CancellationToken ct = default) =>
        RunToolLoopAsync(systemPrompt, new List<ChatMessage> { ChatMessage.FromUserText(userMessage) }, tools, ct);

    /// Phase 4 overload: takes a full conversation (prior turns already flattened to plain
    /// user/assistant text messages by ChatOrchestrationService, plus the new user message already
    /// appended) rather than a single message, so a multi-turn ChatSession's history can be replayed
    /// to the model. `conversation` is mutated in place with this turn's tool_use/tool_result
    /// scaffolding as the loop runs — callers should treat it as owned by this call afterward, not
    /// reuse it, since it now also carries a completed turn's intermediate tool exchange.
    public async Task<ChatConversationResult> RunToolLoopAsync(
        string systemPrompt,
        List<ChatMessage> conversation,
        IReadOnlyList<ChatTool> tools,
        CancellationToken ct = default)
    {
        var toolDefinitions = tools.Select(t => t.Definition).ToList();
        var trace = new List<ChatToolCallTrace>();

        for (var iteration = 1; iteration <= MaxToolIterations; iteration++)
        {
            var response = await chatCompletionService.CompleteAsync(
                new ChatCompletionRequest
                {
                    SystemPrompt = systemPrompt,
                    Messages = conversation,
                    Tools = toolDefinitions,
                },
                ct);

            if (response.StopReason == ChatStopReason.ToolUse)
            {
                conversation.Add(ChatMessage.FromAssistant(response.Content));
                var toolResultBlocks = await ExecuteToolCallsAsync(response.Content, tools, trace, ct);
                conversation.Add(ChatMessage.ToolResults(toolResultBlocks));
                continue;
            }

            if (response.StopReason is ChatStopReason.EndTurn or ChatStopReason.StopSequence)
            {
                var finalText = ExtractText(response.Content);
                return new ChatConversationResult(finalText, trace, response.RawStopReason, iteration, HitIterationLimit: false);
            }

            // Any other stop reason (e.g. "max_tokens", Anthropic's "pause_turn", something a future
            // provider introduces that this loop doesn't specifically know about) — terminate
            // gracefully rather than looping forever or throwing.
            logger.LogWarning("Chat completion provider returned an unhandled stop reason {StopReason}; ending the tool-use loop.", response.RawStopReason);
            var partialText = ExtractText(response.Content);
            return new ChatConversationResult(
                string.IsNullOrWhiteSpace(partialText) ? $"(The assistant stopped early: {response.RawStopReason})" : partialText,
                trace,
                response.RawStopReason,
                iteration,
                HitIterationLimit: false);
        }

        logger.LogWarning("Chat tool-use loop hit the MaxToolIterations cap ({MaxToolIterations}) without reaching end_turn.", MaxToolIterations);
        return new ChatConversationResult(
            "I wasn't able to finish answering that within the allowed number of steps — please try asking something narrower.",
            trace,
            StopReason: "max_iterations",
            Iterations: MaxToolIterations,
            HitIterationLimit: true);
    }

    /// SocChatBot.md Phase 4: the streaming counterpart of RunToolLoopAsync, for
    /// ChatOrchestrationService/ChatController to relay to the widget as SSE. Identical termination
    /// semantics (MaxToolIterations, tool-failure handling, unrecognized-stop-reason handling) —
    /// only "how text arrives" differs: each provider text-delta is surfaced immediately via
    /// ChatStreamEvent.TextDeltaEvent instead of being assembled silently, and tool round trips still
    /// happen synchronously between provider calls (a tool_use turn doesn't itself stream visible
    /// text, so there's nothing to emit until the next provider call produces either more tool_use
    /// or the final answer).
    public async IAsyncEnumerable<ChatStreamEvent> RunToolLoopStreamingAsync(
        string systemPrompt,
        List<ChatMessage> conversation,
        IReadOnlyList<ChatTool> tools,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var toolDefinitions = tools.Select(t => t.Definition).ToList();
        var trace = new List<ChatToolCallTrace>();

        for (var iteration = 1; iteration <= MaxToolIterations; iteration++)
        {
            var finalContent = new List<ChatContentBlock>();
            var stopReason = ChatStopReason.Other;
            string? rawStopReason = null;

            await foreach (var chunk in chatCompletionService.StreamAsync(
                new ChatCompletionRequest { SystemPrompt = systemPrompt, Messages = conversation, Tools = toolDefinitions },
                ct))
            {
                if (chunk.IsTextDelta)
                {
                    yield return ChatStreamEvent.TextDeltaEvent(chunk.TextDelta ?? string.Empty);
                }
                else if (chunk.IsDone)
                {
                    finalContent = chunk.FinalContent ?? new List<ChatContentBlock>();
                    stopReason = chunk.StopReason;
                    rawStopReason = chunk.RawStopReason;
                }
            }

            if (stopReason == ChatStopReason.ToolUse)
            {
                // SocChatBot.md "Phase 5 design decisions": if ANY tool_use block this iteration
                // names a RequiresConfirmation tool, pause the whole iteration — don't execute even a
                // read-only tool_use bundled alongside it in the same response. The assistant's turn
                // still gets appended to `conversation` as usual so ChatOrchestrationService can
                // snapshot it verbatim to resume from later.
                var pendingBlock = finalContent.FirstOrDefault(b =>
                    b.Type == ChatContentBlockType.ToolUse &&
                    tools.FirstOrDefault(t => t.Definition.Name == b.ToolName)?.RequiresConfirmation == true);

                conversation.Add(ChatMessage.FromAssistant(finalContent));

                if (pendingBlock is not null)
                {
                    yield return ChatStreamEvent.PendingActionEvent(
                        pendingBlock.ToolName ?? string.Empty,
                        pendingBlock.ToolInput,
                        pendingBlock.ToolUseId ?? string.Empty);
                    yield break;
                }

                var toolResultBlocks = await ExecuteToolCallsAsync(finalContent, tools, trace, ct);
                conversation.Add(ChatMessage.ToolResults(toolResultBlocks));
                continue;
            }

            if (stopReason is ChatStopReason.EndTurn or ChatStopReason.StopSequence)
            {
                var finalText = ExtractText(finalContent);
                yield return ChatStreamEvent.DoneEvent(new ChatConversationResult(finalText, trace, rawStopReason, iteration, HitIterationLimit: false));
                yield break;
            }

            logger.LogWarning("Chat completion provider returned an unhandled stop reason {StopReason}; ending the tool-use loop.", rawStopReason);
            var partialText = ExtractText(finalContent);
            yield return ChatStreamEvent.DoneEvent(new ChatConversationResult(
                string.IsNullOrWhiteSpace(partialText) ? $"(The assistant stopped early: {rawStopReason})" : partialText,
                trace,
                rawStopReason,
                iteration,
                HitIterationLimit: false));
            yield break;
        }

        logger.LogWarning("Chat tool-use loop hit the MaxToolIterations cap ({MaxToolIterations}) without reaching end_turn.", MaxToolIterations);
        yield return ChatStreamEvent.DoneEvent(new ChatConversationResult(
            "I wasn't able to finish answering that within the allowed number of steps — please try asking something narrower.",
            trace,
            StopReason: "max_iterations",
            Iterations: MaxToolIterations,
            HitIterationLimit: true));
    }

    /// Shared between the streaming and non-streaming loops: executes every tool_use block in
    /// `content` via ExecuteSingleToolCallAsync (idempotencyKey always null here — a
    /// RequiresConfirmation tool never reaches this automatic path, see RunToolLoopStreamingAsync
    /// above), appending every call (success or failure) to `trace`.
    private async Task<List<ChatContentBlock>> ExecuteToolCallsAsync(
        List<ChatContentBlock> content,
        IReadOnlyList<ChatTool> tools,
        List<ChatToolCallTrace> trace,
        CancellationToken ct)
    {
        var toolResultBlocks = new List<ChatContentBlock>();
        foreach (var block in content.Where(b => b.Type == ChatContentBlockType.ToolUse))
        {
            toolResultBlocks.Add(await ExecuteSingleToolCallAsync(block, tools, trace, idempotencyKey: null, ct));
        }

        return toolResultBlocks;
    }

    /// SocChatBot.md Phase 5: extracted out of ExecuteToolCallsAsync (rather than duplicated) so
    /// ChatOrchestrationService.ConfirmActionAsync can execute a single previously-paused tool_use
    /// block for real, on approval — same "unknown tool"/"handler threw" -> is_error tool_result
    /// translation as the ordinary loop, same trace bookkeeping, just for one block instead of a
    /// whole content list. `idempotencyKey` is forwarded to the tool's handler unchanged — every
    /// handler except raise_complaint's ignores it.
    public async Task<ChatContentBlock> ExecuteSingleToolCallAsync(
        ChatContentBlock block,
        IReadOnlyList<ChatTool> tools,
        List<ChatToolCallTrace> trace,
        Guid? idempotencyKey,
        CancellationToken ct)
    {
        var toolName = block.ToolName ?? string.Empty;
        var tool = tools.FirstOrDefault(t => t.Definition.Name == toolName);

        string resultText;
        var isError = false;

        if (tool is null)
        {
            resultText = $"Unknown tool '{toolName}' — no handler is registered for it.";
            isError = true;
            logger.LogWarning("Model requested unknown tool {ToolName} during the tool-use loop.", toolName);
        }
        else
        {
            try
            {
                var input = block.ToolInput ?? JsonDocument.Parse("{}").RootElement;
                resultText = await tool.ExecuteAsync(input, idempotencyKey, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Tool {ToolName} threw during the chat tool-use loop.", toolName);
                resultText = $"The '{toolName}' tool failed and could not return a result. Tell the user this specific lookup isn't available right now.";
                isError = true;
            }
        }

        trace.Add(new ChatToolCallTrace(toolName, block.ToolInput, resultText, isError));
        return ChatContentBlock.ToolResult(block.ToolUseId ?? string.Empty, resultText, isError);
    }

    private static string ExtractText(List<ChatContentBlock> content) =>
        string.Concat(content.Where(b => b.Type == ChatContentBlockType.Text).Select(b => b.Text));
}
