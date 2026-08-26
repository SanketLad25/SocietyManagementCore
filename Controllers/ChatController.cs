using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocietyManagementCore.Models.Dtos;
using SocietyManagementCore.Models.Entities;
using SocietyManagementCore.Services;

namespace SocietyManagementCore.Controllers;

/// SocChatBot.md Phase 4. [Authorize(Roles = RoleNames.ChattableRoles)] is one of the two required
/// SuperAdmin-exclusion layers — the other is ChatOrchestrationService.PrepareTurnAsync's
/// SocietyId-null guard, which runs even though this attribute already excludes SuperAdmin by role,
/// exactly matching this app's stated rule that a role check alone on the controller isn't
/// considered the service-layer guard every other module also carries.
///
/// Streaming/citation design: POST /api/chat/message responds with a plain SSE stream
/// (text/event-stream) of small JSON envelopes rather than raw token text, so the client can tell
/// delta vs. done apart without a second request:
///   event: delta\ndata: {"text":"...partial..."}\n\n      (zero or more)
///   event: done\ndata: {"text":"<full final answer>","citations":[...]}\n\n   (exactly one, last)
/// "done" repeats the complete final answer text (not just the trailing delta) so the client never
/// has to trust that its own accumulated buffer matches what ChatOrchestrationService persisted.
/// Citations ride on the "done" event only, once the whole turn (including every tool round trip)
/// has concluded, rather than trying to attribute them to individual deltas.
[ApiController]
[Route("api/chat")]
[Authorize(Roles = RoleNames.ChattableRoles)]
public class ChatController(ChatOrchestrationService orchestration, ILogger<ChatController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpPost("message")]
    public async Task Message([FromBody] ChatSendMessageRequest request, CancellationToken ct)
    {
        var prepared = await orchestration.PrepareTurnAsync(request.Message, ct);
        if (!prepared.Success)
        {
            Response.StatusCode = StatusCodeForError(prepared.Error);
            await Response.WriteAsJsonAsync(new { message = prepared.Error }, ct);
            return;
        }

        await StreamEventsAsync(orchestration.StreamTurnAsync(prepared.Data!, ct), ct);
    }

    /// SocChatBot.md "Phase 5 design decisions": approves or declines a paused mutating tool_use
    /// (raise_complaint/join_event). Same [Authorize] and SSE response shape as /message — on
    /// approve, the real tool executes and its result feeds back into a freshly-budgeted resumption
    /// of the tool-use loop; on decline, a non-error "the user declined" tool_result does the same.
    [HttpPost("confirm")]
    public async Task Confirm([FromBody] ChatConfirmActionRequest request, CancellationToken ct)
    {
        var prepared = await orchestration.PrepareConfirmAsync(request.Token, request.Approved, ct);
        if (!prepared.Success)
        {
            Response.StatusCode = StatusCodeForConfirmError(prepared.Error);
            await Response.WriteAsJsonAsync(new { message = prepared.Error }, ct);
            return;
        }

        await StreamEventsAsync(orchestration.StreamConfirmAsync(prepared.Data!, ct), ct);
    }

    [HttpGet("history")]
    public async Task<ActionResult<ChatHistoryResponse>> History(CancellationToken ct)
    {
        var result = await orchestration.GetHistoryAsync(ct);
        if (result.Success)
        {
            return Ok(result.Data);
        }

        return StatusCode(StatusCodeForError(result.Error), new { message = result.Error });
    }

    /// Shared by Message and Confirm — both stream the identical delta/done/pending_action/error SSE
    /// envelope shape once past their own ServiceResult-mapped precondition check (SocChatBot.md:
    /// "Stream this exactly like /message").
    private async Task StreamEventsAsync(IAsyncEnumerable<OrchestratedStreamEvent> events, CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        // Harmless when there's no reverse proxy in front (this app's dev setup); prevents an
        // intermediary from buffering the whole response before the client sees anything, should one
        // ever sit in front of this API.
        Response.Headers.Append("X-Accel-Buffering", "no");

        try
        {
            await foreach (var evt in events)
            {
                if (evt.IsTextDelta)
                {
                    await WriteEventAsync("delta", new { text = evt.TextDelta }, ct);
                }
                else if (evt.IsPendingAction)
                {
                    // No "done" event this turn — the widget shows Confirm/Cancel and waits for
                    // POST /api/chat/confirm instead (SocChatBot.md Phase 5).
                    await WriteEventAsync("pending_action", new { token = evt.PendingToken, toolName = evt.PendingToolName, summary = evt.PendingSummary }, ct);
                }
                else if (evt.IsDone)
                {
                    await WriteEventAsync("done", new { text = evt.FinalText, citations = evt.Citations }, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Client navigated away / closed the widget mid-stream — nothing left to write, and not
            // a real error worth logging.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Chat streaming turn failed after the SSE response had already started.");
            // Headers are already sent (Content-Type: text/event-stream) — can't switch to a JSON
            // error response at this point. Emit an "error" SSE event so the widget can at least show
            // something instead of hanging on the typing indicator forever.
            try
            {
                await WriteEventAsync("error", new { message = "The assistant hit an unexpected error. Please try again." }, ct);
            }
            catch
            {
                // Best-effort — the connection may already be gone.
            }
        }
    }

    /// "No society context for this account." is the SuperAdmin-JWT case (403 — the caller is
    /// authenticated but the assistant has nothing to answer about for them); anything else (missing
    /// message text, missing user claim) is a plain bad request. Shared by both actions so the same
    /// failure reason always maps to the same status regardless of which endpoint hit it.
    private static int StatusCodeForError(string? error) => error == "No society context for this account."
        ? StatusCodes.Status403Forbidden
        : StatusCodes.Status400BadRequest;

    /// PrepareConfirmAsync's failure reasons map to 404 (token doesn't identify a confirmation this
    /// caller can see — same response whether it never existed or belongs to someone else, no
    /// enumeration signal either way) or 409 (it exists but is no longer actionable: already
    /// resolved, or expired past the 15-minute TTL) rather than a generic 400.
    private static int StatusCodeForConfirmError(string? error) => error switch
    {
        "No society context for this account." => StatusCodes.Status403Forbidden,
        "Confirmation not found." => StatusCodes.Status404NotFound,
        "This confirmation has already been resolved." or "This confirmation has expired." => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest,
    };

    private async Task WriteEventAsync(string eventName, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await Response.WriteAsync($"event: {eventName}\ndata: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}
