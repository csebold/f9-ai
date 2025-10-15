using System;

namespace ChatClient.Models;

/// <summary>
/// Represents a single chat message shown in the conversation history.
/// </summary>
public sealed record Message(string Author, string Content, DateTimeOffset Timestamp, MessageRole Role)
{
    public bool IsUser => Role == MessageRole.User;

    public bool IsSystem => Role == MessageRole.System;

    public bool IsAssistant => Role == MessageRole.Assistant;
}
