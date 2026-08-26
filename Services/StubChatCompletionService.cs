using System.Text.Json;
using System.Text.RegularExpressions;

namespace SocietyManagementCore.Services;

/// SocChatBot.md "LLM provider abstraction" — a deterministic, scripted fake for
/// IChatCompletionService, added mid-Phase-3 after the configured Anthropic account turned out to
/// have no API credit. No outbound HTTP call, no cost. Everything downstream of "what should the
/// model say next" — ChatToolLoopRunner's orchestration, the MaxToolIterations bound, tool-failure
/// handling, and all real tool handlers (which hit the real database) — runs for real against
/// this stub; only the model's own reasoning is faked.
///
/// Scripting strategy: look at the caller's original question (the first real user-authored text
/// turn — every later "user"-role message in the conversation is actually a wrapped tool_result, not
/// something a human typed) for keywords, and:
///   - if no tool_result for the matching tool is in the history yet, emit a tool_use block for it
///     with plausible arguments (never residentId/societyId/flatId — this stub follows the same
///     input_schema rule the real tools enforce, it simply never has a reason to try otherwise);
///   - once a tool_result for that tool IS in the history, parse it and answer using the actual
///     field values, so the final answer is visibly grounded in real tool output, not a canned string.
///
/// Two additional deterministic trigger phrases exist purely to exercise ChatToolLoopRunner's
/// termination/error-handling paths from a verification harness, without needing a real model:
///   - "keep going" -> always re-requests get_notices, forever, deliberately never reaching
///     end_turn, so the caller's own MaxToolIterations bound (not this stub) is what stops the loop.
///   - "trigger tool failure" -> requests whichever tool in the current request's tool list has
///     "fail" in its name (a harness-only fake tool, never one of the real tools), to prove a
///     thrown tool handler is turned into an is_error tool_result and the loop still concludes
///     gracefully.
public class StubChatCompletionService : IChatCompletionService
{
    private const string LoopForeverTrigger = "keep going";
    private const string ForcedFailureTrigger = "trigger tool failure";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ProviderName => "Stub";

    public Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken ct = default)
    {
        var question = ExtractInitialUserText(request.Messages);
        var toolResultsSoFar = CollectToolResultsByToolName(request.Messages);

        if (question.Contains(LoopForeverTrigger, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(BuildLoopForeverResponse(request));
        }

        if (question.Contains(ForcedFailureTrigger, StringComparison.OrdinalIgnoreCase))
        {
            var forced = BuildForcedFailureResponse(request, toolResultsSoFar);
            if (forced is not null)
            {
                return Task.FromResult(forced);
            }
        }

        var targetTool = DetermineTargetTool(question);
        if (targetTool is not null)
        {
            if (toolResultsSoFar.TryGetValue(targetTool, out var priorResult))
            {
                return Task.FromResult(FinalText(BuildGroundedAnswer(targetTool, priorResult.Content, priorResult.IsError, question)));
            }

            if (request.Tools.Any(t => t.Name == targetTool))
            {
                return Task.FromResult(ToolUseResult(targetTool, BuildArgsForTool(targetTool, question)));
            }
        }

        // No tool keyword matched — this stub has no real model to actually read the system prompt,
        // so fake grounding in the retrieved-context block ChatToolCatalog.BuildSystemPrompt appends
        // (SocChatBot.md Phase 4), rather than always returning the generic "I'm a stub" fallback for
        // every RAG-only question. Real Claude reads the system prompt directly; this is purely so
        // the stub-backed verification exercises a visibly RAG-grounded answer.
        var ragAnswer = BuildAnswerFromRetrievedContext(request.SystemPrompt);
        if (ragAnswer is not null)
        {
            return Task.FromResult(FinalText(ragAnswer));
        }

        return Task.FromResult(FinalText(
            "I'm a stub assistant standing in for the real model during development. Ask about " +
            "published notices, your maintenance dues, the complaints in your society, or the " +
            "status of a specific complaint (by number)."));
    }

    private static string? BuildAnswerFromRetrievedContext(string systemPrompt)
    {
        var headerIndex = systemPrompt.IndexOf(ChatToolCatalog.RetrievedContextHeader, StringComparison.Ordinal);
        if (headerIndex < 0)
        {
            return null;
        }

        var excerpts = systemPrompt[(headerIndex + ChatToolCatalog.RetrievedContextHeader.Length)..].Trim();
        if (string.IsNullOrWhiteSpace(excerpts))
        {
            return null;
        }

        // Just the first excerpt (chunks are separated by "---" in
        // ChatOrchestrationService.BuildRetrievedContextBlock) — enough to prove the answer is
        // actually derived from what was retrieved, not a canned string.
        var firstExcerpt = excerpts.Split("---", StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        return string.IsNullOrWhiteSpace(firstExcerpt)
            ? null
            : $"Based on your society's published notices/knowledge base: {firstExcerpt}";
    }

    // ---- Scenario: forced max-iterations ----

    private static ChatCompletionResult BuildLoopForeverResponse(ChatCompletionRequest request)
    {
        if (!request.Tools.Any(t => t.Name == "get_notices"))
        {
            return FinalText("I don't have the get_notices tool available to keep checking.");
        }

        // Deliberately ALWAYS returns tool_use for get_notices, even once a get_notices tool_result
        // is already present in the history — this scenario exists specifically to prove
        // ChatToolLoopRunner's MaxToolIterations bound is what ends the loop, not the model
        // "deciding" to stop.
        return ToolUseResult("get_notices", "{}");
    }

    // ---- Scenario: forced tool failure ----

    private static ChatCompletionResult? BuildForcedFailureResponse(
        ChatCompletionRequest request, IReadOnlyDictionary<string, (string Content, bool IsError)> toolResultsSoFar)
    {
        var failingToolName = request.Tools.FirstOrDefault(t => t.Name.Contains("fail", StringComparison.OrdinalIgnoreCase))?.Name;
        if (failingToolName is null)
        {
            return null;
        }

        if (toolResultsSoFar.TryGetValue(failingToolName, out var result))
        {
            return FinalText(
                result.IsError
                    ? $"As expected, the '{failingToolName}' tool failed: {result.Content}"
                    : $"The '{failingToolName}' tool actually succeeded, which wasn't expected for this test: {result.Content}");
        }

        return ToolUseResult(failingToolName, "{}");
    }

    // ---- Real tool routing ----

    private static string? DetermineTargetTool(string question)
    {
        var q = question.ToLowerInvariant();

        // SocChatBot.md Phase 5: checked before the generic "complaint" branch below, so "raise a
        // complaint about..." routes to the mutating tool rather than the read-only status/list ones.
        if (q.Contains("raise") && q.Contains("complaint"))
        {
            return "raise_complaint";
        }

        if (q.Contains("complaint categor"))
        {
            return "get_complaint_categories";
        }

        if (q.Contains("complaint"))
        {
            // A message that names a specific complaint number ("complaint 5", "status of complaint
            // #5") routes to the single-item lookup; anything else that just mentions complaints in
            // general ("what are all my complaints", "what complaints do I have", or simply
            // "complaint" with no number) routes to the list tool instead of guessing complaintId: 1.
            return Regex.IsMatch(question, @"\d+") ? "get_complaint_status" : "get_my_complaints";
        }

        if (q.Contains("maintenance") || q.Contains("due"))
        {
            return "get_my_maintenance_dues";
        }

        if (q.Contains("notice"))
        {
            return "get_notices";
        }

        // Checked before the generic "event" branch so "join event 3" routes to the mutating tool
        // rather than the read-only list.
        if (q.Contains("join") && q.Contains("event"))
        {
            return "join_event";
        }

        if (q.Contains("event"))
        {
            return "get_my_events";
        }

        if (q.Contains("parking"))
        {
            return "get_parking_details";
        }

        return null;
    }

    private static string BuildArgsForTool(string toolName, string question)
    {
        switch (toolName)
        {
            case "get_complaint_status":
            {
                var match = Regex.Match(question, @"\d+");
                var complaintId = match.Success ? match.Value : "1";
                return $$"""{"complaintId": {{complaintId}}}""";
            }

            case "join_event":
            {
                var match = Regex.Match(question, @"\d+");
                var eventId = match.Success ? match.Value : "1";
                return $$"""{"eventId": {{eventId}}, "participantCount": 1}""";
            }

            case "raise_complaint":
            {
                var match = Regex.Match(question, @"\d+");
                var categoryId = match.Success ? match.Value : "1";
                var description = JsonSerializer.Serialize(question);
                return $$"""{"categoryId": {{categoryId}}, "description": {{description}}, "priority": "Medium"}""";
            }

            default:
                return "{}";
        }
    }

    /// Parses the real JSON a tool handler returned and builds an answer that quotes it, so the
    /// "final answer" step of this stub is genuinely grounded, not a canned string that happens to
    /// follow a tool call.
    private static string BuildGroundedAnswer(string toolName, string resultJson, bool isError, string question)
    {
        if (isError)
        {
            return $"I tried to look that up, but the tool reported a failure: {resultJson}";
        }

        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            var root = doc.RootElement;

            switch (toolName)
            {
                case "get_notices":
                    if (root.TryGetProperty("notices", out var notices) && notices.ValueKind == JsonValueKind.Array && notices.GetArrayLength() > 0)
                    {
                        var titles = notices.EnumerateArray()
                            .Select(n => n.TryGetProperty("title", out var t) ? t.GetString() : null)
                            .Where(t => !string.IsNullOrWhiteSpace(t));
                        return $"Here are the latest published notices for your society: {string.Join("; ", titles)}.";
                    }

                    return root.TryGetProperty("message", out var noticesMsg) ? noticesMsg.GetString() ?? "No notices found." : "No notices found.";

                case "get_my_maintenance_dues":
                    if (root.TryGetProperty("totalOutstanding", out var total))
                    {
                        var flatNo = root.TryGetProperty("flatNo", out var f) ? f.GetString() : "your flat";

                        // "due date"/"last due date"/"when is it due" asks about a specific bill
                        // field the tool result already carries (per-bill DueDate) — answer that
                        // directly instead of always falling back to the totalOutstanding template,
                        // which was ignoring the actual question asked.
                        var q = question.ToLowerInvariant();
                        if ((q.Contains("due date") || q.Contains("when") || q.Contains("last due")) &&
                            root.TryGetProperty("bills", out var billsForDate) &&
                            billsForDate.ValueKind == JsonValueKind.Array &&
                            billsForDate.GetArrayLength() > 0)
                        {
                            var latestDueDate = billsForDate.EnumerateArray()
                                .Select(b => b.TryGetProperty("dueDate", out var d) ? d.GetString() : null)
                                .Where(d => !string.IsNullOrWhiteSpace(d))
                                .Max();

                            return latestDueDate is not null
                                ? $"For flat {flatNo}, the most recent maintenance bill's due date is {latestDueDate}."
                                : $"For flat {flatNo}, none of the published bills have a recorded due date.";
                        }

                        var billCount = root.TryGetProperty("bills", out var bills) && bills.ValueKind == JsonValueKind.Array
                            ? bills.GetArrayLength()
                            : 0;
                        return $"For flat {flatNo}, your total outstanding maintenance due across {billCount} published bill(s) is {total}.";
                    }

                    return root.TryGetProperty("message", out var duesMsg) ? duesMsg.GetString() ?? "No maintenance dues found." : "No maintenance dues found.";

                case "get_my_complaints":
                    if (root.TryGetProperty("complaints", out var complaintsArr) && complaintsArr.ValueKind == JsonValueKind.Array && complaintsArr.GetArrayLength() > 0)
                    {
                        var items = complaintsArr.EnumerateArray().Select(c =>
                        {
                            var id = c.TryGetProperty("complaintId", out var cid) ? cid.GetInt32() : 0;
                            var category = c.TryGetProperty("categoryName", out var cat) ? cat.GetString() : "Unknown";
                            var itemStatus = c.TryGetProperty("status", out var st) ? st.GetString() ?? "Unknown" : "Unknown";
                            var assignedTo = c.TryGetProperty("assignedToName", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() : null;
                            var raisedBy = c.TryGetProperty("residentName", out var rn) && rn.ValueKind == JsonValueKind.String ? rn.GetString() : null;

                            var statusText = assignedTo is null
                                ? itemStatus
                                : itemStatus.Contains("assign", StringComparison.OrdinalIgnoreCase)
                                    ? $"{itemStatus} to {assignedTo}"
                                    : $"{itemStatus}, assigned to {assignedTo}";

                            var raisedByText = string.IsNullOrEmpty(raisedBy) ? "" : $", raised by {raisedBy}";

                            return $"#{id} ({category}) — {statusText}{raisedByText}";
                        });

                        return $"You have {complaintsArr.GetArrayLength()} complaint(s): {string.Join("; ", items)}.";
                    }

                    return root.TryGetProperty("message", out var complaintsMsg) ? complaintsMsg.GetString() ?? "No complaints found." : "No complaints found.";

                case "get_complaint_status":
                    if (root.TryGetProperty("status", out var status) && root.TryGetProperty("complaintId", out var complaintId))
                    {
                        var category = root.TryGetProperty("categoryName", out var cat) ? cat.GetString() : "an unspecified category";
                        var raisedBy = root.TryGetProperty("residentName", out var rn) && rn.ValueKind == JsonValueKind.String
                            ? rn.GetString()
                            : null;
                        var raisedByText = string.IsNullOrEmpty(raisedBy) ? "" : $" Raised by {raisedBy}.";
                        return $"Complaint #{complaintId.GetInt32()} ({category}) currently has status '{status.GetString()}'.{raisedByText}";
                    }

                    return root.TryGetProperty("message", out var complaintMsg) ? complaintMsg.GetString() ?? "That complaint could not be found." : "That complaint could not be found.";

                case "get_my_events":
                    if (root.TryGetProperty("events", out var eventsArr) && eventsArr.ValueKind == JsonValueKind.Array && eventsArr.GetArrayLength() > 0)
                    {
                        var names = eventsArr.EnumerateArray()
                            .Select(e => e.TryGetProperty("eventName", out var n) ? n.GetString() : null)
                            .Where(n => !string.IsNullOrWhiteSpace(n));
                        return $"Upcoming events in your society: {string.Join("; ", names)}.";
                    }

                    return root.TryGetProperty("message", out var eventsMsg) ? eventsMsg.GetString() ?? "No events found." : "No events found.";

                case "get_complaint_categories":
                    if (root.TryGetProperty("categories", out var catsArr) && catsArr.ValueKind == JsonValueKind.Array && catsArr.GetArrayLength() > 0)
                    {
                        var names = catsArr.EnumerateArray()
                            .Select(c => c.TryGetProperty("categoryName", out var n) ? n.GetString() : null)
                            .Where(n => !string.IsNullOrWhiteSpace(n));
                        return $"Available complaint categories: {string.Join(", ", names)}.";
                    }

                    return root.TryGetProperty("message", out var catsMsg) ? catsMsg.GetString() ?? "No complaint categories found." : "No complaint categories found.";

                case "get_parking_details":
                    if (root.TryGetProperty("parkings", out var parkingsArr) && parkingsArr.ValueKind == JsonValueKind.Array && parkingsArr.GetArrayLength() > 0)
                    {
                        var flatNo = root.TryGetProperty("flatNo", out var f) ? f.GetString() : "your flat";
                        return $"Flat {flatNo} has {parkingsArr.GetArrayLength()} parking slot(s) assigned.";
                    }

                    return root.TryGetProperty("message", out var parkingMsg) ? parkingMsg.GetString() ?? "No parking details found." : "No parking details found.";

                case "join_event":
                case "raise_complaint":
                    // Both are mutating tools whose handlers already return a human-readable
                    // {success, message} shape (see ChatToolCatalog) — no further parsing needed.
                    return root.TryGetProperty("message", out var actionMsg) ? actionMsg.GetString() ?? "Done." : "Done.";

                default:
                    return $"The '{toolName}' tool returned: {resultJson}";
            }
        }
        catch (JsonException)
        {
            return $"The '{toolName}' tool returned: {resultJson}";
        }
    }

    // ---- Content-history helpers ----

    /// Phase 4 fix: was `FirstOrDefault`, which is only correct for a single-turn conversation (all
    /// of Phase 3's verification). Once ChatOrchestrationService feeds prior turns back in as
    /// history (SocChatBot.md "Load recent ChatMessage history... append the new user message"),
    /// the first "user"-role text turn in the list is the OLDEST question in the session, not the
    /// current one. A "user"-role message wrapping tool_result blocks has no Text-type content, so
    /// it's naturally excluded from this filter either way — `LastOrDefault` here always finds the
    /// most recent actual human-typed message, which for a single-turn conversation is identical to
    /// the old `FirstOrDefault` behavior (there's only one), so this is a strict fix, not a
    /// behavior change for anything already verified.
    private static string ExtractInitialUserText(List<ChatMessage> messages) =>
        messages
            .LastOrDefault(m => m.Role == ChatRole.User && m.Content.Any(c => c.Type == ChatContentBlockType.Text))
            ?.Content.Where(c => c.Type == ChatContentBlockType.Text)
            .Select(c => c.Text ?? string.Empty)
            .FirstOrDefault() ?? string.Empty;

    /// Pairs every ToolUse block (assistant turns) with its matching ToolResult block (user turns,
    /// by id) across the whole conversation so far, keyed by tool name.
    private static Dictionary<string, (string Content, bool IsError)> CollectToolResultsByToolName(List<ChatMessage> messages)
    {
        var idToToolName = new Dictionary<string, string>();
        foreach (var block in messages.Where(m => m.Role == ChatRole.Assistant).SelectMany(m => m.Content))
        {
            if (block.Type == ChatContentBlockType.ToolUse && block.ToolUseId is not null && block.ToolName is not null)
            {
                idToToolName[block.ToolUseId] = block.ToolName;
            }
        }

        var results = new Dictionary<string, (string, bool)>();
        foreach (var block in messages.Where(m => m.Role == ChatRole.User).SelectMany(m => m.Content))
        {
            if (block.Type == ChatContentBlockType.ToolResult && block.ToolResultForId is not null &&
                idToToolName.TryGetValue(block.ToolResultForId, out var toolName))
            {
                results[toolName] = (block.ToolResultContent ?? string.Empty, block.ToolResultIsError);
            }
        }

        return results;
    }

    // ---- Result builders ----

    private static ChatCompletionResult ToolUseResult(string toolName, string argsJson)
    {
        var input = JsonDocument.Parse(argsJson).RootElement.Clone();
        var id = $"stub_{Guid.NewGuid():N}";
        return new ChatCompletionResult
        {
            StopReason = ChatStopReason.ToolUse,
            RawStopReason = "tool_use",
            Content = new List<ChatContentBlock> { ChatContentBlock.ToolUse(id, toolName, input) },
        };
    }

    private static ChatCompletionResult FinalText(string text) => new()
    {
        StopReason = ChatStopReason.EndTurn,
        RawStopReason = "end_turn",
        Content = new List<ChatContentBlock> { ChatContentBlock.TextBlock(text) },
    };

    /// IChatCompletionService.StreamAsync — promoted onto the interface in Phase 4 (see
    /// ClaudeClient.StreamAsync's doc comment: ChatOrchestrationService needs to invoke whichever
    /// provider DI resolves, which requires real interface dispatch, not a duck-typed method).
    /// Fakes streaming purely for plumbing verification purposes: runs CompleteAsync for the given
    /// turn, then re-emits any text content word-by-word as delta chunks before a final Done chunk.
    public async IAsyncEnumerable<ChatStreamChunk> StreamAsync(
        ChatCompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var result = await CompleteAsync(request, ct);

        foreach (var block in result.Content.Where(b => b.Type == ChatContentBlockType.Text))
        {
            foreach (var word in (block.Text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                ct.ThrowIfCancellationRequested();
                yield return ChatStreamChunk.TextDeltaChunk(word + " ");
                await Task.Yield();
            }
        }

        yield return ChatStreamChunk.DoneChunk(result.StopReason, result.RawStopReason, result.Content);
    }
}
