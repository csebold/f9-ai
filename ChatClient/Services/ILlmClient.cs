using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Models;

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
    public LlmRequest(
        string prompt,
        string? instructions = null,
        bool includeInstructions = false,
        IEnumerable<LlmMessage>? history = null)
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

        History = history is null
            ? Array.Empty<LlmMessage>()
            : history
                .Where(static message => message is not null && !string.IsNullOrWhiteSpace(message.Content))
                .Select(static message => new LlmMessage(message.Role, message.Content))
                .ToArray();
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

    /// <summary>
    /// Full conversation history to send with the request, including the latest user prompt.
    /// </summary>
    public IReadOnlyList<LlmMessage> History { get; }
}

/// <summary>
/// Represents a single message to send to a chat completion API.
/// </summary>
public sealed record LlmMessage
{
    public LlmMessage(MessageRole role, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content cannot be null or whitespace.", nameof(content));
        }

        Role = role;
        Content = content.Trim();
    }

    public MessageRole Role { get; }

    public string Content { get; }
}
