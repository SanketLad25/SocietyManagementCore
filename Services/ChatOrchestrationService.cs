using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SocietyManagementCore.Data;
using SocietyManagementCore.Models.Dtos;

// Entities.ChatSession / Entities.ChatMessageRecord below — an explicit alias, not a stylistic
// choice: this file also uses the unqualified "ChatMessage" name throughout for
// Services.ChatMessage (the provider-neutral LLM-turn DTO from IChatCompletionService.cs, same
// namespace as this class), so the persisted DB entity (Models.Entities.ChatMessageRecord, mapped
// onto the real ChatMessage table) needs to stay qualified to avoid ambiguity. See
// ChatMessageRecord.cs's doc comment for the full collision story.
using Entities = SocietyManagementCore.Models.Entities;

namespace SocietyManagementCore.Services;

/// SocChatBot.md Phase 4: the core of the chatbot feature — wires retrieval (Phase 2), the tool-use
/// loop (Phase 3), and per-session persistence together for a single chat turn. Split into
/// PrepareTurnAsync (resolves the SocietyId-null guard, loads/creates the session and history, runs
/// retrieval, builds the system prompt) and StreamTurnAsync (drives the streaming tool-use loop and
/// persists the exchange once it concludes) so ChatController can reject a SuperAdmin/no-society
/// caller with an ordinary ServiceResult-mapped HTTP status *before* committing to an SSE response,
/// while the actual generation is still fully streamed.
public class ChatOrchestrationService(
    SocietyDbContext db,
    ICurrentUserContext currentUser,
    IRetrievalService retrievalService,
    ChatToolCatalog toolCatalog,
    ChatToolLoopRunner toolLoopRunner,
    NoticeService noticeService,
    KnowledgeArticleService knowledgeArticleService,
    ComplaintCategoryService complaintCategoryService,
    EventService eventService,
    ILogger<ChatOrchestrationService> logger)
{
    /// SocChatBot.md "Phase 5 design decisions": TTL checked at confirm time
    /// (ServiceResult.Fail if expired).
    private const int PendingActionTtlMinutes = 15;

    /// How many DocumentChunk rows IRetrievalService.SearchAsync returns per turn.
    private const int RetrievalTopK = 4;

    /// Cosine distance (0 = identical, larger = less similar) above which a retrieved chunk is
    /// treated as noise rather than a real match — applied uniformly to both what's injected into
    /// the system prompt and what's surfaced as a citation, so an unrelated question (e.g.
    /// "what's my maintenance due") never drags in an irrelevant notice as if it were a source.
    /// Chosen empirically against this app's real ingested content during Phase 4 verification
    /// (voyage-2 embeddings, one "Guest Parking Policy" KnowledgeArticle chunk ingested for the test
    /// society): "what is the guest parking policy?" (genuinely relevant) scored 0.2272; "what is my
    /// maintenance due?" and "check status of complaint 5" (both irrelevant) scored 0.3776/0.4218.
    /// An initial guess of 0.75 was too loose — it let both irrelevant questions cite the parking
    /// article — caught by that same verification run. 0.30 sits with margin above the one relevant
    /// data point and below both irrelevant ones; revisit with more calibration examples once the
    /// knowledge base has more than one article, or if the content mix changes a lot.
    private const double RelevanceDistanceThreshold = 0.30;

    /// How many prior ChatMessageRecord rows (both roles combined) are replayed as history on each
    /// turn. An informal cap ahead of SocChatBot.md Phase 6's formal "summarize/drop older turns"
    /// control — this just bounds the worst case (an extremely long-lived session) from growing the
    /// per-request payload unboundedly in the meantime.
    private const int HistoryMessageLimit = 20;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// Everything StreamTurnAsync needs to drive the tool-use loop and persist the result, computed
    /// up front by PrepareTurnAsync so the SocietyId-null guard can fail before any SSE headers are
    /// written.
    public class PreparedTurn
    {
        public required Entities.ChatSession Session { get; init; }
        public required string SystemPrompt { get; init; }
        public required List<ChatMessage> Conversation { get; init; }
        public required IReadOnlyList<ChatTool> Tools { get; init; }
        public required IReadOnlyList<ChatCitationResponse> Citations { get; init; }
        public required string UserMessage { get; init; }
    }

    /// SocChatBot.md "ChatOrchestrationService's SocietyId-null guard": the actual server-side
    /// SuperAdmin enforcement, independent of ChatController's [Authorize(Roles=ChattableRoles)] —
    /// a SuperAdmin's JWT carries no SocietyId, so this fails before any retrieval/generation work
    /// starts (and before ChatController commits to an SSE response).
    public async Task<ServiceResult<PreparedTurn>> PrepareTurnAsync(string userMessage, CancellationToken ct = default)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<PreparedTurn>.Fail("No society context for this account.");
        }

        if (currentUser.UserId is not { } userId)
        {
            return ServiceResult<PreparedTurn>.Fail("No authenticated user context.");
        }

        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return ServiceResult<PreparedTurn>.Fail("Message text is required.");
        }

        var session = await GetOrCreateSessionAsync(societyId, userId, ct);
        var history = await LoadHistoryAsync(session.ChatSessionId, ct);

        var conversation = history
            .Select(m => m.Role == Entities.ChatMessageRoles.Assistant
                ? ChatMessage.FromAssistantText(m.Content)
                : ChatMessage.FromUserText(m.Content))
            .ToList();
        conversation.Add(ChatMessage.FromUserText(userMessage));

        // IRetrievalService.SearchAsync calls out to the embedding provider (Voyage AI) and, unlike
        // IngestionService, has no ServiceResult wrapper of its own (SocChatBot.md Phase 2 only
        // guarded the ingestion path) — a provider outage/rate-limit would otherwise take down the
        // entire chat turn, including the tool-based answer, for a purely supplementary RAG lookup.
        // Found empirically during Phase 4 verification (a real 429 from Voyage's free tier), not a
        // hypothetical: degrade to "no retrieved context this turn" instead of failing the turn.
        List<RetrievedChunk> relevantChunks;
        try
        {
            var allChunks = await retrievalService.SearchAsync(societyId, userMessage, RetrievalTopK, ct);
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Retrieval distances for {SocietyId}/{Query}: {Distances}",
                    societyId, userMessage, string.Join(", ", allChunks.Select(c => $"{c.SourceType}#{c.SourceId}={c.Distance:F4}")));
            }

            relevantChunks = allChunks.Where(c => c.Distance <= RelevanceDistanceThreshold).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Retrieval failed for society {SocietyId}; continuing this turn with no retrieved context.", societyId);
            relevantChunks = new List<RetrievedChunk>();
        }

        var citations = await ResolveCitationsAsync(relevantChunks, ct);

        var societyName = await db.Societies
            .Where(s => s.SocietyId == societyId)
            .Select(s => s.SocietyName)
            .FirstOrDefaultAsync(ct) ?? "your society";

        var retrievedContext = BuildRetrievedContextBlock(relevantChunks);
        var systemPrompt = ChatToolCatalog.BuildSystemPrompt(societyName, retrievedContext);

        return ServiceResult<PreparedTurn>.Ok(new PreparedTurn
        {
            Session = session,
            SystemPrompt = systemPrompt,
            Conversation = conversation,
            Tools = toolCatalog.BuildTools(),
            Citations = citations,
            UserMessage = userMessage,
        });
    }

    /// The final answer text + citations, streamed as OrchestratedStreamEvent instances, then
    /// persisted as a ChatMessageRecord pair once the tool-use loop concludes. Delegates to
    /// DriveLoopAsync, shared with StreamConfirmAsync (SocChatBot.md Phase 5) so both the initial
    /// turn and a confirm-triggered resumption handle a pending-action pause identically.
    public IAsyncEnumerable<OrchestratedStreamEvent> StreamTurnAsync(PreparedTurn turn, CancellationToken ct = default) =>
        DriveLoopAsync(turn.Session, turn.UserMessage, turn.SystemPrompt, turn.Conversation, turn.Tools, turn.Citations, ct);

    /// Everything StreamConfirmAsync needs to resume a paused tool-use loop, computed up front by
    /// PrepareConfirmAsync so an invalid/expired/already-resolved token can fail with an ordinary
    /// ServiceResult-mapped HTTP status before any SSE headers are written — same split as
    /// PrepareTurnAsync/StreamTurnAsync above.
    public class PreparedConfirm
    {
        public required Entities.ChatSession Session { get; init; }
        public required string SystemPrompt { get; init; }
        public required List<ChatMessage> Conversation { get; init; }
        public required IReadOnlyList<ChatTool> Tools { get; init; }
        public required string OriginalUserMessage { get; init; }
    }

    /// SocChatBot.md "Phase 5 design decisions": loads the ChatPendingAction row the token
    /// identifies, verifies it belongs to the caller's own SocietyId/session and is still Pending
    /// and unexpired, then flips its Status to Confirmed/Cancelled via a single conditional UPDATE
    /// (WHERE Status = Pending) — not read-then-write — so the retry-race window is actually closed:
    /// a second near-simultaneous call with the same token loses that UPDATE (rowsUpdated == 0) and
    /// fails here instead of also proceeding. Then executes the real tool (approved) or builds a
    /// declined tool_result (not approved) for every tool_use block from the paused iteration —
    /// including any read-only tool bundled alongside the confirmation-gated one, which was withheld
    /// for the same reason and is now safe to actually run.
    public async Task<ServiceResult<PreparedConfirm>> PrepareConfirmAsync(string token, bool approved, CancellationToken ct = default)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<PreparedConfirm>.Fail("No society context for this account.");
        }

        if (currentUser.UserId is not { } userId)
        {
            return ServiceResult<PreparedConfirm>.Fail("No authenticated user context.");
        }

        if (!Guid.TryParse(token, out var tokenGuid))
        {
            return ServiceResult<PreparedConfirm>.Fail("Invalid confirmation token.");
        }

        var pending = await db.ChatPendingActions.FirstOrDefaultAsync(p => p.IdempotencyKey == tokenGuid && p.SocietyId == societyId, ct);
        if (pending is null)
        {
            return ServiceResult<PreparedConfirm>.Fail("Confirmation not found.");
        }

        var session = await db.ChatSessions.FirstOrDefaultAsync(
            s => s.ChatSessionId == pending.ChatSessionId && s.SocietyId == societyId && s.UserId == userId, ct);
        if (session is null)
        {
            // Belongs to a different caller/society, or the session was somehow removed — same
            // not-found response as an unrecognized token, no enumeration signal either way.
            return ServiceResult<PreparedConfirm>.Fail("Confirmation not found.");
        }

        if (pending.Status != Entities.ChatPendingActionStatuses.Pending)
        {
            return ServiceResult<PreparedConfirm>.Fail("This confirmation has already been resolved.");
        }

        if (pending.ExpiresOn < DateTime.UtcNow)
        {
            await db.ChatPendingActions
                .Where(p => p.ChatPendingActionId == pending.ChatPendingActionId && p.Status == Entities.ChatPendingActionStatuses.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, Entities.ChatPendingActionStatuses.Expired), ct);
            return ServiceResult<PreparedConfirm>.Fail("This confirmation has expired.");
        }

        // Single conditional UPDATE (WHERE Status = Pending), not read-then-write: two
        // near-simultaneous confirm calls on the same token can both have read Status == Pending
        // above, but only one's UPDATE can actually affect the row — the loser's rowsUpdated == 0
        // and it fails the same way an already-resolved token would, instead of both proceeding to
        // execute the tool.
        var newStatus = approved ? Entities.ChatPendingActionStatuses.Confirmed : Entities.ChatPendingActionStatuses.Cancelled;
        var rowsUpdated = await db.ChatPendingActions
            .Where(p => p.ChatPendingActionId == pending.ChatPendingActionId && p.Status == Entities.ChatPendingActionStatuses.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, newStatus), ct);

        if (rowsUpdated == 0)
        {
            return ServiceResult<PreparedConfirm>.Fail("This confirmation has already been resolved.");
        }

        List<ChatMessage> conversation;
        try
        {
            conversation = JsonSerializer.Deserialize<List<ChatMessage>>(pending.ConversationSnapshotJson, JsonOptions) ?? new List<ChatMessage>();
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize ChatPendingAction {ChatPendingActionId}'s conversation snapshot.", pending.ChatPendingActionId);
            return ServiceResult<PreparedConfirm>.Fail("This confirmation's saved conversation could not be restored.");
        }

        var pausedToolUseBlocks = conversation
            .LastOrDefault(m => m.Role == ChatRole.Assistant)
            ?.Content.Where(b => b.Type == ChatContentBlockType.ToolUse).ToList()
            ?? new List<ChatContentBlock>();

        var tools = toolCatalog.BuildTools();
        var trace = new List<ChatToolCallTrace>();
        var toolResultBlocks = new List<ChatContentBlock>();

        foreach (var block in pausedToolUseBlocks)
        {
            if (block.ToolUseId == pending.ToolUseId)
            {
                if (approved)
                {
                    // Only meaningful for raise_complaint (SocChatBot.md Phase 5) — every other
                    // tool's handler ignores this parameter.
                    var idempotencyKey = pending.ToolName == "raise_complaint" ? pending.IdempotencyKey : (Guid?)null;
                    toolResultBlocks.Add(await toolLoopRunner.ExecuteSingleToolCallAsync(block, tools, trace, idempotencyKey, ct));
                }
                else
                {
                    toolResultBlocks.Add(ChatContentBlock.ToolResult(
                        block.ToolUseId ?? string.Empty, "The user declined to proceed with this action.", isError: false));
                }
            }
            else if (tools.FirstOrDefault(t => t.Definition.Name == block.ToolName)?.RequiresConfirmation == true)
            {
                // A SECOND confirmation-gated tool_use bundled into the same paused iteration (e.g.
                // the model tried to both raise_complaint and join_event in one turn). The summary
                // the resident actually saw/approved only ever describes the ONE tool named by
                // `pending` — silently executing this one too under that same approval would be
                // exactly the unconfirmed-mutation gap this whole feature exists to close. Never
                // auto-execute it; tell the model plainly instead of pretending it ran.
                toolResultBlocks.Add(ChatContentBlock.ToolResult(
                    block.ToolUseId ?? string.Empty,
                    "This action was not confirmed and was not executed. Ask about it again on its own so the user can explicitly confirm it.",
                    isError: false));
            }
            else
            {
                // A read-only tool_use bundled into the same paused iteration (SocChatBot.md:
                // "pause the ENTIRE iteration, even if a read-only tool_use appears alongside it") —
                // never auto-executed while the confirmation was outstanding. The gate is resolved
                // now (approved or not), so it's safe to actually run it for real.
                toolResultBlocks.Add(await toolLoopRunner.ExecuteSingleToolCallAsync(block, tools, trace, idempotencyKey: null, ct));
            }
        }

        conversation.Add(ChatMessage.ToolResults(toolResultBlocks));

        // Same "last User-role, text-only message" technique StubChatCompletionService's
        // ExtractInitialUserText doc comment explains: a "user"-role message wrapping tool_result
        // blocks has no Text-type content, so it's naturally excluded, leaving the actual human
        // question this whole turn started from — regardless of how many prior turns or confirm
        // round trips are already in the snapshot.
        var originalUserMessage = conversation
            .LastOrDefault(m => m.Role == ChatRole.User && m.Content.Any(c => c.Type == ChatContentBlockType.Text))
            ?.Content.Where(c => c.Type == ChatContentBlockType.Text).Select(c => c.Text ?? string.Empty).FirstOrDefault()
            ?? string.Empty;

        var societyName = await db.Societies
            .Where(s => s.SocietyId == societyId)
            .Select(s => s.SocietyName)
            .FirstOrDefaultAsync(ct) ?? "your society";

        // No retrieval is re-run for a confirm-triggered resumption — the resident isn't asking a new
        // question, they're approving/declining a proposed action, so there's nothing new to ground.
        var systemPrompt = ChatToolCatalog.BuildSystemPrompt(societyName);

        return ServiceResult<PreparedConfirm>.Ok(new PreparedConfirm
        {
            Session = session,
            SystemPrompt = systemPrompt,
            Conversation = conversation,
            Tools = tools,
            OriginalUserMessage = originalUserMessage,
        });
    }

    /// Resumes the tool-use loop with a fresh MaxToolIterations budget, streamed exactly like
    /// StreamTurnAsync (delta/done/pending_action, since the model could itself propose another
    /// confirmation-gated action right after this one resolves) — no new citations are computed for
    /// this leg, since no retrieval was re-run.
    public IAsyncEnumerable<OrchestratedStreamEvent> StreamConfirmAsync(PreparedConfirm prepared, CancellationToken ct = default) =>
        DriveLoopAsync(prepared.Session, prepared.OriginalUserMessage, prepared.SystemPrompt, prepared.Conversation, prepared.Tools, Array.Empty<ChatCitationResponse>(), ct);

    /// Shared by StreamTurnAsync and StreamConfirmAsync: drives ChatToolLoopRunner, passing text
    /// deltas straight through, persisting + yielding Done once the loop actually concludes, and —
    /// SocChatBot.md Phase 5 — persisting a new ChatPendingAction row and yielding a PendingAction
    /// event (ending the stream) if the loop pauses on a confirmation-gated tool instead.
    private async IAsyncEnumerable<OrchestratedStreamEvent> DriveLoopAsync(
        Entities.ChatSession session,
        string originalUserMessage,
        string systemPrompt,
        List<ChatMessage> conversation,
        IReadOnlyList<ChatTool> tools,
        IReadOnlyList<ChatCitationResponse> citations,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var evt in toolLoopRunner.RunToolLoopStreamingAsync(systemPrompt, conversation, tools, ct))
        {
            if (evt.IsTextDelta)
            {
                yield return OrchestratedStreamEvent.TextDeltaEvent(evt.TextDelta ?? string.Empty);
                continue;
            }

            if (evt.IsPendingAction)
            {
                var token = Guid.NewGuid();
                var summary = await BuildConfirmationSummaryAsync(evt.PendingToolName ?? string.Empty, evt.PendingToolInput, session.SocietyId, ct);

                db.ChatPendingActions.Add(new Entities.ChatPendingAction
                {
                    SocietyId = session.SocietyId,
                    ChatSessionId = session.ChatSessionId,
                    ToolName = evt.PendingToolName ?? string.Empty,
                    ToolInputJson = evt.PendingToolInput?.GetRawText() ?? "{}",
                    ToolUseId = evt.PendingToolUseId ?? string.Empty,
                    ConversationSnapshotJson = JsonSerializer.Serialize(conversation, JsonOptions),
                    IdempotencyKey = token,
                    Status = Entities.ChatPendingActionStatuses.Pending,
                    CreatedOn = DateTime.UtcNow,
                    ExpiresOn = DateTime.UtcNow.AddMinutes(PendingActionTtlMinutes),
                });
                await db.SaveChangesAsync(ct);

                yield return OrchestratedStreamEvent.PendingActionEvent(token.ToString(), evt.PendingToolName ?? string.Empty, summary);
                yield break;
            }

            var result = evt.Result!;
            await PersistExchangeAsync(session, originalUserMessage, citations, result, ct);
            yield return OrchestratedStreamEvent.DoneEvent(result.FinalText, citations);
        }
    }

    /// Human-readable confirmation prompt for the resident to approve/decline (SocChatBot.md Phase 5
    /// examples: 'Raise a complaint: "..." (Category: X, Priority: Y)?' /
    /// 'Join event "X" with N participant(s)?'). Looks up the category/event name via the same
    /// services the tool handlers themselves use (ComplaintCategoryService/EventService), never a new
    /// direct query against a tenant-scoped table.
    private async Task<string> BuildConfirmationSummaryAsync(string toolName, JsonElement? inputOpt, int societyId, CancellationToken ct)
    {
        var input = inputOpt ?? JsonDocument.Parse("{}").RootElement;

        if (toolName == "raise_complaint")
        {
            var description = input.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String
                ? d.GetString() ?? string.Empty
                : string.Empty;
            var priority = input.TryGetProperty("priority", out var p) && p.ValueKind == JsonValueKind.String
                ? p.GetString() ?? Entities.ComplaintPriorities.Medium
                : Entities.ComplaintPriorities.Medium;

            var categoryName = "an unspecified category";
            if (input.TryGetProperty("categoryId", out var c) && c.TryGetInt32(out var categoryId))
            {
                var categories = await complaintCategoryService.ListAsync();
                categoryName = categories.FirstOrDefault(x => x.ComplaintCategoryId == categoryId)?.CategoryName ?? categoryName;
            }

            var snippet = description.Length > 80 ? description[..80] + "..." : description;
            return $"Raise a complaint: \"{snippet}\" (Category: {categoryName}, Priority: {priority})?";
        }

        if (toolName == "join_event")
        {
            var participantCount = input.TryGetProperty("participantCount", out var pc) && pc.TryGetInt32(out var n) ? n : 1;

            var eventName = "this event";
            if (input.TryGetProperty("eventId", out var e) && e.TryGetInt32(out var eventId))
            {
                var ev = await eventService.GetAsync(eventId);
                eventName = ev?.EventName ?? eventName;
            }

            return $"Join event \"{eventName}\" with {participantCount} participant(s)?";
        }

        // Not expected today (only raise_complaint/join_event are RequiresConfirmation), but a
        // graceful fallback rather than throwing if a future confirmation-gated tool is added without
        // updating this method.
        return $"Proceed with {toolName}?";
    }

    /// GET-history support for ChatWidget.jsx to restore a session's transcript on mount (the widget
    /// itself holds no server-side state between page loads otherwise). No SocChatBot.md
    /// conversation-history *sidebar* is in scope (that decision was about picking an old session /
    /// starting a new one) — this is just replaying the single continuous session's own transcript.
    ///
    /// SocChatBot.md Phase 5: also surfaces a still-Pending, non-expired ChatPendingAction for this
    /// session, if any, so reopening the widget mid-confirmation doesn't lose the Confirm/Cancel UI —
    /// the resident closing the panel before responding shouldn't silently abandon the proposal.
    public async Task<ServiceResult<ChatHistoryResponse>> GetHistoryAsync(CancellationToken ct = default)
    {
        if (currentUser.SocietyId is not { } societyId)
        {
            return ServiceResult<ChatHistoryResponse>.Fail("No society context for this account.");
        }

        if (currentUser.UserId is not { } userId)
        {
            return ServiceResult<ChatHistoryResponse>.Fail("No authenticated user context.");
        }

        var session = await db.ChatSessions
            .Where(s => s.SocietyId == societyId && s.UserId == userId)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (session is null)
        {
            return ServiceResult<ChatHistoryResponse>.Ok(new ChatHistoryResponse { Messages = new List<ChatHistoryEntryResponse>() });
        }

        var rows = await db.ChatMessageRecords
            .Where(m => m.ChatSessionId == session.ChatSessionId)
            .OrderBy(m => m.ChatMessageId)
            .ToListAsync(ct);

        var entries = rows.Select(m => new ChatHistoryEntryResponse
        {
            Role = m.Role,
            Content = m.Content,
            CreatedOn = m.CreatedOn,
            Citations = string.IsNullOrWhiteSpace(m.Citations)
                ? null
                : JsonSerializer.Deserialize<List<ChatCitationResponse>>(m.Citations, JsonOptions),
        }).ToList();

        var now = DateTime.UtcNow;
        var pending = await db.ChatPendingActions
            .Where(p => p.ChatSessionId == session.ChatSessionId && p.Status == Entities.ChatPendingActionStatuses.Pending && p.ExpiresOn > now)
            .OrderByDescending(p => p.CreatedOn)
            .FirstOrDefaultAsync(ct);

        ChatPendingActionResponse? pendingResponse = null;
        if (pending is not null)
        {
            var input = JsonDocument.Parse(pending.ToolInputJson).RootElement;
            var summary = await BuildConfirmationSummaryAsync(pending.ToolName, input, societyId, ct);
            pendingResponse = new ChatPendingActionResponse(pending.IdempotencyKey.ToString(), pending.ToolName, summary);
        }

        return ServiceResult<ChatHistoryResponse>.Ok(new ChatHistoryResponse { Messages = entries, PendingAction = pendingResponse });
    }

    private async Task<Entities.ChatSession> GetOrCreateSessionAsync(int societyId, int userId, CancellationToken ct)
    {
        var session = await db.ChatSessions
            .Where(s => s.SocietyId == societyId && s.UserId == userId)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (session is not null)
        {
            return session;
        }

        session = new Entities.ChatSession
        {
            SocietyId = societyId,
            UserId = userId,
            ResidentId = currentUser.ResidentId,
            StartedAt = DateTime.UtcNow,
        };

        db.ChatSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return session;
    }

    private async Task<List<Entities.ChatMessageRecord>> LoadHistoryAsync(int chatSessionId, CancellationToken ct)
    {
        var rows = await db.ChatMessageRecords
            .Where(m => m.ChatSessionId == chatSessionId)
            .OrderByDescending(m => m.ChatMessageId)
            .Take(HistoryMessageLimit)
            .ToListAsync(ct);

        rows.Reverse();
        return rows;
    }

    /// SocChatBot.md "Citation metadata gap, close it here": RetrievedChunk carries SourceType/
    /// SourceId but no title, so resolve it per chunk from the owning service. A lookup coming back
    /// null (source unpublished/deleted between ingestion and now) is omitted, not an error — this
    /// should be rare given the delete-on-unpublish path built in Phase 2.
    private async Task<List<ChatCitationResponse>> ResolveCitationsAsync(IReadOnlyList<RetrievedChunk> chunks, CancellationToken ct)
    {
        var citations = new List<ChatCitationResponse>();
        var seen = new HashSet<(string SourceType, int SourceId)>();

        foreach (var chunk in chunks)
        {
            ct.ThrowIfCancellationRequested();
            var key = (chunk.SourceType, chunk.SourceId);
            if (!seen.Add(key))
            {
                continue; // multiple chunks from the same source -> one citation
            }

            string? title = chunk.SourceType switch
            {
                Entities.DocumentChunkSourceTypes.Notice => (await noticeService.GetAsync(chunk.SourceId))?.Title,
                Entities.DocumentChunkSourceTypes.KnowledgeArticle => (await knowledgeArticleService.GetPublishedAsync(chunk.SourceId))?.Title,
                _ => null,
            };

            if (title is not null)
            {
                citations.Add(new ChatCitationResponse(chunk.SourceType, chunk.SourceId, title));
            }
            else
            {
                logger.LogInformation(
                    "Omitting citation for {SourceType} {SourceId} — source no longer resolves (unpublished/deleted since ingestion).",
                    chunk.SourceType, chunk.SourceId);
            }
        }

        return citations;
    }

    private static string? BuildRetrievedContextBlock(IReadOnlyList<RetrievedChunk> chunks)
    {
        if (chunks.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder();
        sb.AppendLine(ChatToolCatalog.RetrievedContextHeader);
        foreach (var chunk in chunks)
        {
            sb.AppendLine("---");
            sb.AppendLine(chunk.ChunkText);
        }

        return sb.ToString();
    }

    /// Widened from taking a PreparedTurn (Phase 4) to explicit primitives (Phase 5) so
    /// DriveLoopAsync can call this identically whether the turn started fresh (StreamTurnAsync) or
    /// resumed from a confirmed/declined pending action (StreamConfirmAsync) — the latter has no
    /// PreparedTurn, only a PreparedConfirm.
    private async Task PersistExchangeAsync(
        Entities.ChatSession session, string userMessage, IReadOnlyList<ChatCitationResponse> citations, ChatConversationResult result, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        db.ChatMessageRecords.Add(new Entities.ChatMessageRecord
        {
            SocietyId = session.SocietyId,
            ChatSessionId = session.ChatSessionId,
            Role = Entities.ChatMessageRoles.User,
            Content = userMessage,
            CreatedOn = now,
        });

        db.ChatMessageRecords.Add(new Entities.ChatMessageRecord
        {
            SocietyId = session.SocietyId,
            ChatSessionId = session.ChatSessionId,
            Role = Entities.ChatMessageRoles.Assistant,
            Content = result.FinalText,
            Citations = citations.Count > 0 ? JsonSerializer.Serialize(citations, JsonOptions) : null,
            CreatedOn = now,
        });

        await db.SaveChangesAsync(ct);
    }
}

/// StreamTurnAsync/StreamConfirmAsync's output — text deltas pass straight through from
/// ChatToolLoopRunner's ChatStreamEvent; Done is orchestration-specific (adds Citations, since
/// ChatToolLoopRunner has no knowledge of retrieval); PendingAction (SocChatBot.md Phase 5) carries
/// the confirmation token/tool name/human-readable summary for ChatController to relay verbatim as
/// the "pending_action" SSE event.
public class OrchestratedStreamEvent
{
    public bool IsTextDelta { get; init; }
    public string? TextDelta { get; init; }
    public bool IsDone { get; init; }
    public string? FinalText { get; init; }
    public IReadOnlyList<ChatCitationResponse>? Citations { get; init; }
    public bool IsPendingAction { get; init; }
    public string? PendingToken { get; init; }
    public string? PendingToolName { get; init; }
    public string? PendingSummary { get; init; }

    public static OrchestratedStreamEvent TextDeltaEvent(string text) => new() { IsTextDelta = true, TextDelta = text };

    public static OrchestratedStreamEvent DoneEvent(string finalText, IReadOnlyList<ChatCitationResponse> citations) =>
        new() { IsDone = true, FinalText = finalText, Citations = citations };

    public static OrchestratedStreamEvent PendingActionEvent(string token, string toolName, string summary) =>
        new() { IsPendingAction = true, PendingToken = token, PendingToolName = toolName, PendingSummary = summary };
}
