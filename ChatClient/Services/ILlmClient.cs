using System.Threading;
using System.Threading.Tasks;

namespace ChatClient.Services;

/// <summary>
/// Abstraction for requesting completions from a large language model provider.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Sends the supplied prompt to the configured LLM provider and returns the assistant's response.
    /// </summary>
    Task<string> GetResponseAsync(string prompt, CancellationToken cancellationToken = default);
}
