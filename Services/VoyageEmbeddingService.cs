using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SocietyManagementCore.Options;

namespace SocietyManagementCore.Services;

/// Every Voyage AI-specific detail (request/response shape, auth header, endpoint URL) lives only
/// here — IngestionService/IRetrievalService never reference this class directly, only
/// IEmbeddingService (SocChatBot.md "Embedding provider abstraction"). Registered as a typed client
/// (`builder.Services.AddHttpClient<VoyageEmbeddingService>()`), this app's first outbound HttpClient
/// alongside the Phase-3 ClaudeClient.
///
/// Best-known shape of Voyage AI's embeddings API (unverified against a real API key in this
/// environment — see Program.cs registration comment / the Phase 2 report):
///   POST https://api.voyageai.com/v1/embeddings
///   Header: Authorization: Bearer {ApiKey}
///   Body:   { "input": ["text1", "text2"], "model": "voyage-2", "input_type": "document" | "query" }
///   Resp:   { "data": [ { "embedding": [0.1, 0.2, ...], "index": 0 }, ... ] }
/// If the actual response doesn't match this shape at runtime, this class fails loudly with the raw
/// response body in the exception message rather than silently returning garbage — a shape mismatch
/// here would otherwise be very hard to diagnose anywhere downstream in the ingestion pipeline.
public class VoyageEmbeddingService : IEmbeddingService
{
    private const string EmbeddingsPath = "v1/embeddings";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient httpClient;
    private readonly EmbeddingOptions options;

    public VoyageEmbeddingService(HttpClient httpClient, IOptions<EmbeddingOptions> options)
    {
        this.httpClient = httpClient;
        this.options = options.Value;

        if (this.httpClient.BaseAddress is null)
        {
            this.httpClient.BaseAddress = new Uri("https://api.voyageai.com/");
        }

        if (!string.IsNullOrWhiteSpace(this.options.ApiKey))
        {
            this.httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", this.options.ApiKey);
        }
    }

    public string ProviderName => "Voyage";

    public string ModelName => options.Model;

    public int Dimensions => options.Dimensions;

    public async Task<float[]> EmbedAsync(string text, EmbeddingKind kind, CancellationToken ct = default)
    {
        var results = await EmbedBatchAsync(new[] { text }, kind, ct);
        return results[0];
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, EmbeddingKind kind, CancellationToken ct = default)
    {
        if (texts.Count == 0)
        {
            return Array.Empty<float[]>();
        }

        var requestBody = new VoyageEmbeddingRequest
        {
            Input = texts.ToArray(),
            Model = options.Model,
            InputType = kind == EmbeddingKind.Document ? "document" : "query",
        };

        using var response = await httpClient.PostAsJsonSafeAsync(EmbeddingsPath, requestBody, JsonOptions, ct);
        var rawBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Voyage AI embeddings call failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}. " +
                $"Raw response body: {rawBody}");
        }

        VoyageEmbeddingResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<VoyageEmbeddingResponse>(rawBody, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Voyage AI embeddings response could not be parsed as JSON. Raw response body: {rawBody}", ex);
        }

        if (parsed?.Data is null || parsed.Data.Count == 0)
        {
            throw new InvalidOperationException(
                $"Voyage AI embeddings response had no usable 'data' array. Raw response body: {rawBody}");
        }

        if (parsed.Data.Count != texts.Count)
        {
            throw new InvalidOperationException(
                $"Voyage AI embeddings response returned {parsed.Data.Count} embedding(s) for {texts.Count} input(s) " +
                $"— cannot safely align results to inputs. Raw response body: {rawBody}");
        }

        // Voyage documents an "index" field per item; defensively sort on it (falling back to
        // request order if it's ever absent) rather than assuming the array is already in order.
        var ordered = parsed.Data.OrderBy(d => d.Index).ToList();

        var vectors = new List<float[]>(ordered.Count);
        foreach (var item in ordered)
        {
            if (item.Embedding is null || item.Embedding.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Voyage AI embeddings response contained an item with no 'embedding' array. Raw response body: {rawBody}");
            }

            if (item.Embedding.Length != options.Dimensions)
            {
                throw new InvalidOperationException(
                    $"Voyage AI returned a {item.Embedding.Length}-dimension embedding but " +
                    $"Embedding:Dimensions is configured as {options.Dimensions}. Raw response body: {rawBody}");
            }

            vectors.Add(item.Embedding);
        }

        return vectors;
    }

    private class VoyageEmbeddingRequest
    {
        [JsonPropertyName("input")]
        public string[] Input { get; set; } = Array.Empty<string>();

        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("input_type")]
        public string InputType { get; set; } = string.Empty;
    }

    private class VoyageEmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<VoyageEmbeddingItem>? Data { get; set; }
    }

    private class VoyageEmbeddingItem
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }

        [JsonPropertyName("index")]
        public int Index { get; set; }
    }
}

internal static class HttpClientJsonExtensions
{
    /// Thin wrapper so a malformed request body still surfaces the raw response rather than
    /// throwing from inside System.Net.Http.Json before we get a chance to read the body text.
    public static async Task<HttpResponseMessage> PostAsJsonSafeAsync<T>(
        this HttpClient client, string requestUri, T body, JsonSerializerOptions jsonOptions, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(body, jsonOptions);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return await client.PostAsync(requestUri, content, ct);
    }
}
