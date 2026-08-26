namespace SocietyManagementCore.Services;

/// The only embeddings abstraction IngestionService/IRetrievalService are allowed to depend on —
/// no Voyage-specific (or any other provider's) type or API shape may leak past this interface, so
/// a future move to Azure OpenAI is a config change + a re-embedding pass, not a rewrite
/// (SocChatBot.md "Embedding provider abstraction").
public interface IEmbeddingService
{
    /// Stamped onto ChunkEmbedding.EmbeddingProvider at write time — lets a re-embed job select
    /// "every row not produced by the currently configured provider+model" with a plain WHERE.
    string ProviderName { get; }

    /// Stamped onto ChunkEmbedding.EmbeddingModel at write time.
    string ModelName { get; }

    /// Must match ChunkEmbedding.Embedding's VECTOR(n) column width for the currently configured
    /// provider/model.
    int Dimensions { get; }

    Task<float[]> EmbedAsync(string text, EmbeddingKind kind, CancellationToken ct = default);

    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, EmbeddingKind kind, CancellationToken ct = default);
}

/// Distinguishes "this text is a stored document chunk" from "this text is a live search query" —
/// most embedding APIs (Voyage included) produce measurably better retrieval when document and
/// query text are embedded with different input-type hints, even though both land in the same
/// vector space. IngestionService always passes Document; IRetrievalService always passes Query.
public enum EmbeddingKind
{
    Document,
    Query,
}
