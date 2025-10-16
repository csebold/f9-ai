using System;
using System.Collections.Generic;

namespace ChatClient.Models;

public sealed class SessionStoreSnapshot
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public List<ProjectSessionSnapshot> Projects { get; set; } = new();
}

public sealed class ProjectSessionSnapshot
{
    public string ProjectId { get; set; } = string.Empty;

    public string? ActiveSessionId { get; set; }

    public List<ChatSessionSnapshot> Sessions { get; set; } = new();
}

public sealed class ChatSessionSnapshot
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Title { get; set; } = "New Chat";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<Message> Messages { get; set; } = new();
}
