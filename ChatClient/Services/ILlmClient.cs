using System;
using System.Threading;
using System.Threading.Tasks;

namespace ChatClient.Services;

/// <summary>
/// Abstraction for requesting completions from a large language model provider.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Sends the supplied request to the configured LLM provider and returns the assistant's response.
    /// </summary>
    Task<string> GetResponseAsync(LlmRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a single LLM completion request.
/// </summary>
public sealed class LlmRequest
{
    public LlmRequest(string prompt, string? instructions = null, bool includeInstructions = false)
    {
        Prompt = string.IsNullOrWhiteSpace(prompt)
            ? throw new ArgumentException("Prompt cannot be null or whitespace.", nameof(prompt))
            : prompt;

        if (string.IsNullOrWhiteSpace(instructions))
        {
            Instructions = null;
            IncludeInstructions = false;
        }
        else
        {
            Instructions = instructions.Trim();
            IncludeInstructions = includeInstructions;
        }
    }

    /// <summary>
    /// The user-authored prompt content.
    /// </summary>
    public string Prompt { get; }

    /// <summary>
    /// Optional project instructions to send as system context.
    /// </summary>
    public string? Instructions { get; }

    /// <summary>
    /// Indicates whether the instructions should be included with this request.
    /// </summary>
    public bool IncludeInstructions { get; }
}
