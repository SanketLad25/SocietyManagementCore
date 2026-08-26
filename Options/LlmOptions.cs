namespace SocietyManagementCore.Options;

/// SocChatBot.md "LLM provider abstraction" — same shape as EmbeddingOptions.Provider: a plain
/// config key resolves which IChatCompletionService implementation Program.cs registers, so
/// swapping "Stub" for "Anthropic" (once Anthropic:ApiKey has real credit) is a config change only,
/// never a code change.
public class LlmOptions
{
    /// "Stub" | "Anthropic". Defaults to "Stub" in appsettings.Development.json until real API
    /// credit is available.
    public string Provider { get; set; } = "Stub";
}
