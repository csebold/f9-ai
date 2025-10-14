using System;

namespace ChatClient.Models;

/// <summary>
/// Represents a single chat message shown in the conversation history.
/// </summary>
public sealed record Message(string Author, string Content, DateTimeOffset Timestamp, bool IsUser);
