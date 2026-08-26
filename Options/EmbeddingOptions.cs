namespace SocietyManagementCore.Options;

public class EmbeddingOptions
{
    public string Provider { get; set; } = "Voyage";   // "Voyage" | "AzureOpenAI"
    public string Model { get; set; } = "voyage-2";
    public string ApiKey { get; set; } = string.Empty;
    public int Dimensions { get; set; } = 1024;

    // Populated only when Provider == "AzureOpenAI" — unused, harmless, for the Voyage path:
    public string? AzureEndpoint { get; set; }
    public string? AzureDeploymentName { get; set; }
    public string? AzureApiVersion { get; set; }
}
