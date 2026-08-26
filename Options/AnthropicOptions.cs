namespace SocietyManagementCore.Options;

public class AnthropicOptions
{
    public string ApiKey { get; set; } = string.Empty;

    // "claude-sonnet-4-5" in an earlier draft of SocChatBot.md was a stale model ID — corrected to
    // the model actually available to this account.
    public string Model { get; set; } = "claude-sonnet-5";
}
