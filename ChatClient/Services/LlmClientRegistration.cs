using System;

namespace ChatClient.Services;

/// <summary>
/// Represents a configured LLM client alongside metadata used for UI status updates.
/// </summary>
public sealed record LlmClientRegistration(ILlmClient Client, LlmProvider Provider, string ProviderDisplayName, string ModelId, string? StatusDetail = null)
{
    public Uri? Endpoint { get; init; }
}
