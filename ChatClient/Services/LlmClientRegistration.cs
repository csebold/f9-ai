namespace ChatClient.Services;

/// <summary>
/// Represents a configured LLM client alongside metadata used for UI status updates.
/// </summary>
public sealed record LlmClientRegistration(ILlmClient Client, string ProviderDisplayName, string ModelId, string? StatusDetail = null);
