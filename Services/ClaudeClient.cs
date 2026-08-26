using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SocietyManagementCore.Options;

namespace SocietyManagementCore.Services;

/// SocChatBot.md Phase 3: wraps Anthropic's Messages API (https://api.anthropic.com/v1/messages)
/// over HttpClient. Registered as a typed client (`builder.Services.AddHttpClient&lt;ClaudeClient&gt;()`
/// in Program.cs) — this backend's second outbound HttpClient after VoyageEmbeddingService, same
/// socket-exhaustion reasoning.
///
/// This class owns the raw request/response (and streaming) shape of the Anthropic Messages API and
/// nothing else — it implements IChatCompletionService, translating to/from that interface's neutral
/// ChatCompletionRequest/ChatCompletionResult shape, so no Anthropic-specific type (ClaudeMessage,
/// the wire-shaped ClaudeContentBlock, ClaudeConversationMessage, etc.) is ever visible outside this
/// file. The generic tool-use loop mechanics (the explicit termination bound, tool-failure handling,
/// unrecognized-stop_reason handling) live in ChatToolLoopRunner, which depends only on
/// IChatCompletionService — never on ClaudeClient directly — mirroring the discipline
/// IngestionService already follows toward VoyageEmbeddingService via IEmbeddingService.
public class ClaudeClient : IChatCompletionService
{
    private const string MessagesPath = "v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    private const int DefaultMaxTokens = 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient httpClient;
    private readonly AnthropicOptions options;
    private readonly ILogger<ClaudeClient> logger;

    public ClaudeClient(HttpClient httpClient, IOptions<AnthropicOptions> options, ILogger<ClaudeClient> logger)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
        this.logger = logger;

        if (this.httpClient.BaseAddress is null)
        {
            this.httpClient.BaseAddress = new Uri("https://api.anthropic.com/");
        }

        if (!string.IsNullOrWhiteSpace(this.options.ApiKey) && !this.httpClient.DefaultRequestHeaders.Contains("x-api-key"))
        {
            this.httpClient.DefaultRequestHeaders.Add("x-api-key", this.options.ApiKey);
        }

        if (!this.httpClient.DefaultRequestHeaders.Contains("anthropic-version"))
        {
            this.httpClient.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);
        }
    }

    /// IChatCompletionService.ProviderName — stamped nowhere today (no per-message provider
    /// tracking exists yet for chat, unlike ChunkEmbedding's EmbeddingProvider/EmbeddingModel
    /// columns), but kept on the interface for parity with IEmbeddingService and for logging.
    public string ProviderName => "Anthropic";

    /// IChatCompletionService.CompleteAsync — translates the neutral request shape to Anthropic's
    /// wire format, sends it, and translates the response back. This is the only method of
    /// ClaudeClient that ChatToolLoopRunner (or anything else depending on IChatCompletionService)
    /// is allowed to call.
    public async Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken ct = default)
    {
        var wireMessages = request.Messages.Select(ToWireMessage).ToList();
        var wireTools = request.Tools.Select(ToWireToolDefinition).ToList();
        var response = await SendRawAsync(request.SystemPrompt, wireMessages, wireTools, ct);
        return ToNeutralResult(response);
    }

    private static ClaudeConversationMessage ToWireMessage(ChatMessage message) => new()
    {
        Role = message.Role == ChatRole.User ? "user" : "assistant",
        Content = message.Content.Select(ToWireContentBlock).ToList(),
    };

    private static ClaudeContentBlock ToWireContentBlock(ChatContentBlock block) => block.Type switch
    {
        ChatContentBlockType.Text => ClaudeContentBlock.TextBlock(block.Text ?? string.Empty),
        ChatContentBlockType.ToolUse => new ClaudeContentBlock
        {
            Type = "tool_use",
            Id = block.ToolUseId,
            Name = block.ToolName,
            Input = block.ToolInput,
        },
        ChatContentBlockType.ToolResult => ClaudeContentBlock.ToolResult(
            block.ToolResultForId ?? string.Empty, block.ToolResultContent ?? string.Empty, block.ToolResultIsError),
        _ => throw new InvalidOperationException($"Unknown ChatContentBlockType '{block.Type}'."),
    };

    private static ClaudeToolDefinition ToWireToolDefinition(ChatToolDefinition definition) => new()
    {
        Name = definition.Name,
        Description = definition.Description,
        InputSchema = definition.InputSchema,
    };

    private static ChatContentBlock ToNeutralContentBlock(ClaudeContentBlock block) => block.Type switch
    {
        "tool_use" => ChatContentBlock.ToolUse(
            block.Id ?? string.Empty, block.Name ?? string.Empty, block.Input ?? JsonDocument.Parse("{}").RootElement),
        // "text" and anything else this app hasn't special-cased yet (e.g. a future "thinking" block
        // type) are surfaced as plain text rather than dropped silently.
        _ => ChatContentBlock.TextBlock(block.Text ?? string.Empty),
    };

    private static ChatStopReason ToNeutralStopReason(string? rawStopReason) => rawStopReason switch
    {
        "end_turn" => ChatStopReason.EndTurn,
        "tool_use" => ChatStopReason.ToolUse,
        "max_tokens" => ChatStopReason.MaxTokens,
        "stop_sequence" => ChatStopReason.StopSequence,
        _ => ChatStopReason.Other,
    };

    private static ChatCompletionResult ToNeutralResult(ClaudeMessage message) => new()
    {
        StopReason = ToNeutralStopReason(message.StopReason),
        RawStopReason = message.StopReason,
        Content = message.Content.Select(ToNeutralContentBlock).ToList(),
    };

    /// Single non-streaming Messages API round trip, in Anthropic's own wire format. Throws (loudly,
    /// with the raw response body) on a non-2xx or unparseable response — same "fail loud, don't
    /// return garbage" posture as VoyageEmbeddingService. Private: everything outside this class
    /// talks to ClaudeClient exclusively through CompleteAsync (IChatCompletionService).
    private async Task<ClaudeMessage> SendRawAsync(
        string systemPrompt,
        List<ClaudeConversationMessage> messages,
        IReadOnlyList<ClaudeToolDefinition> tools,
        CancellationToken ct = default)
    {
        var body = new ClaudeMessageRequestBody
        {
            Model = options.Model,
            MaxTokens = DefaultMaxTokens,
            System = systemPrompt,
            Messages = messages,
            Tools = tools.Count > 0 ? tools.ToList() : null,
            Stream = false,
        };

        var json = JsonSerializer.Serialize(body, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync(MessagesPath, content, ct);
        var rawBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Anthropic Messages API call failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}. " +
                $"Raw response body: {rawBody}");
        }

        ClaudeMessage? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ClaudeMessage>(rawBody, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Anthropic Messages API response could not be parsed as JSON. Raw response body: {rawBody}", ex);
        }

        if (parsed is null)
        {
            throw new InvalidOperationException($"Anthropic Messages API returned an empty/unparseable body: {rawBody}");
        }

        parsed.RawJson = rawBody;
        return parsed;
    }

    /// IChatCompletionService.StreamAsync — streams a single Messages API turn as SSE, yielding text
    /// deltas as they arrive plus a final "done" chunk carrying the fully-assembled content blocks
    /// (including any tool_use blocks, whose `input` is accumulated from `input_json_delta` chunks
    /// and parsed once complete). Promoted onto the interface in Phase 4 (SocChatBot.md
    /// ChatOrchestrationService needs to invoke whichever provider DI resolves, not ClaudeClient
    /// specifically) — translates Anthropic's wire shape to the neutral ChatStreamChunk/
    /// ChatContentBlock types via the same ToNeutralContentBlock/ToNeutralStopReason helpers
    /// CompleteAsync uses, so no Anthropic-specific type is visible outside this file.
    public async IAsyncEnumerable<ChatStreamChunk> StreamAsync(
        ChatCompletionRequest chatRequest,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var wireMessages = chatRequest.Messages.Select(ToWireMessage).ToList();
        var wireTools = chatRequest.Tools.Select(ToWireToolDefinition).ToList();
        var systemPrompt = chatRequest.SystemPrompt;

        var body = new ClaudeMessageRequestBody
        {
            Model = options.Model,
            MaxTokens = DefaultMaxTokens,
            System = systemPrompt,
            Messages = wireMessages,
            Tools = wireTools.Count > 0 ? wireTools : null,
            Stream = true,
        };

        var json = JsonSerializer.Serialize(body, JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, MessagesPath)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Anthropic Messages API streaming call failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}. " +
                $"Raw response body: {errorBody}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        var textBlocks = new SortedDictionary<int, StringBuilder>();
        var toolBlocks = new SortedDictionary<int, (string Id, string Name, StringBuilder PartialJson)>();
        string? stopReason = null;

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null)
            {
                break;
            }

            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue; // "event: ..." lines and blank keep-alive lines — the "type" field inside data is enough
            }

            var data = line["data:".Length..].Trim();
            if (string.IsNullOrEmpty(data) || data == "[DONE]")
            {
                continue;
            }

            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;
            var eventType = root.GetProperty("type").GetString();

            switch (eventType)
            {
                case "content_block_start":
                {
                    var index = root.GetProperty("index").GetInt32();
                    var block = root.GetProperty("content_block");
                    var blockType = block.GetProperty("type").GetString();
                    if (blockType == "text")
                    {
                        textBlocks[index] = new StringBuilder(block.TryGetProperty("text", out var t) ? t.GetString() ?? string.Empty : string.Empty);
                    }
                    else if (blockType == "tool_use")
                    {
                        toolBlocks[index] = (
                            block.GetProperty("id").GetString() ?? string.Empty,
                            block.GetProperty("name").GetString() ?? string.Empty,
                            new StringBuilder());
                    }

                    break;
                }
                case "content_block_delta":
                {
                    var index = root.GetProperty("index").GetInt32();
                    var delta = root.GetProperty("delta");
                    var deltaType = delta.GetProperty("type").GetString();
                    if (deltaType == "text_delta")
                    {
                        var textChunk = delta.GetProperty("text").GetString() ?? string.Empty;
                        if (!textBlocks.TryGetValue(index, out var sb))
                        {
                            sb = new StringBuilder();
                            textBlocks[index] = sb;
                        }

                        sb.Append(textChunk);
                        yield return ChatStreamChunk.TextDeltaChunk(textChunk);
                    }
                    else if (deltaType == "input_json_delta")
                    {
                        var partial = delta.GetProperty("partial_json").GetString() ?? string.Empty;
                        if (toolBlocks.TryGetValue(index, out var tb))
                        {
                            tb.PartialJson.Append(partial);
                        }
                    }

                    break;
                }
                case "message_delta":
                {
                    if (root.TryGetProperty("delta", out var messageDelta) &&
                        messageDelta.TryGetProperty("stop_reason", out var stopReasonProp) &&
                        stopReasonProp.ValueKind == JsonValueKind.String)
                    {
                        stopReason = stopReasonProp.GetString();
                    }

                    break;
                }
                case "error":
                {
                    var errorDetail = root.TryGetProperty("error", out var errorEl) ? errorEl.ToString() : data;
                    throw new InvalidOperationException($"Anthropic streaming returned an error event: {errorDetail}");
                }
            }
        }

        var finalContent = new List<ChatContentBlock>();
        foreach (var (_, sb) in textBlocks)
        {
            finalContent.Add(ChatContentBlock.TextBlock(sb.ToString()));
        }

        foreach (var (_, tool) in toolBlocks)
        {
            var jsonStr = tool.PartialJson.Length == 0 ? "{}" : tool.PartialJson.ToString();
            JsonElement inputElement;
            try
            {
                inputElement = JsonDocument.Parse(jsonStr).RootElement.Clone();
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Could not parse streamed tool_use input JSON for tool {ToolName}: {Raw}", tool.Name, jsonStr);
                inputElement = JsonDocument.Parse("{}").RootElement.Clone();
            }

            finalContent.Add(ChatContentBlock.ToolUse(tool.Id, tool.Name, inputElement));
        }

        yield return ChatStreamChunk.DoneChunk(ToNeutralStopReason(stopReason), stopReason, finalContent);
    }

}

// ---- Messages API request/response DTOs ----

public class ClaudeMessageRequestBody
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    [JsonPropertyName("system")]
    public string System { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<ClaudeConversationMessage> Messages { get; set; } = new();

    [JsonPropertyName("tools")]
    public List<ClaudeToolDefinition>? Tools { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

public class ClaudeToolDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// A plain JSON-Schema-shaped object (usually an anonymous type) — serialized as-is. Per
    /// SocChatBot.md's "Tool input_schema rule", none of this app's tool schemas may declare
    /// residentId/societyId/flatId as a property; both are resolved from ICurrentUserContext inside
    /// the handler, never accepted as a tool-call argument.
    [JsonPropertyName("input_schema")]
    public object InputSchema { get; set; } = new { type = "object", properties = new { } };
}

public class ClaudeConversationMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public List<ClaudeContentBlock> Content { get; set; } = new();
}

/// One entry of a message's `content` array. Anthropic's Messages API reuses the same content-block
/// shape for text, tool_use (assistant asking to call a tool), and tool_result (the reply the caller
/// sends back, as a "user"-role message) — a single class with nullable fields is the simplest
/// mapping instead of a discriminated hierarchy, mirroring how loosely-typed the wire shape actually
/// is.
public class ClaudeContentBlock
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    // tool_use fields (assistant -> caller)
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("input")]
    public JsonElement? Input { get; set; }

    // tool_result fields (caller -> assistant, sent inside a "user" role message)
    [JsonPropertyName("tool_use_id")]
    public string? ToolUseId { get; set; }

    [JsonPropertyName("content")]
    public string? ResultContent { get; set; }

    [JsonPropertyName("is_error")]
    public bool? IsError { get; set; }

    public static ClaudeContentBlock TextBlock(string text) => new() { Type = "text", Text = text };

    public static ClaudeContentBlock ToolResult(string toolUseId, string resultText, bool isError) =>
        new()
        {
            Type = "tool_result",
            ToolUseId = toolUseId,
            ResultContent = resultText,
            IsError = isError ? true : null,
        };
}

public class ClaudeUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}

public class ClaudeMessage
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public List<ClaudeContentBlock> Content { get; set; } = new();

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }

    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; set; }

    [JsonPropertyName("usage")]
    public ClaudeUsage? Usage { get; set; }

    [JsonIgnore]
    public string? RawJson { get; set; }
}

// Streaming's neutral chunk type (ChatStreamChunk) now lives in IChatCompletionService.cs, alongside
// ChatCompletionResult — promoted onto the interface in Phase 4 (see StreamAsync's doc comment
// above). Tool-use loop plumbing (ChatTool, ChatToolCallTrace, ChatConversationResult) lives in
// ChatToolLoopRunner.cs. Nothing provider-neutral is left in this file.
