using System;
using ChatClient.Services;

namespace ChatClient.Models;

public sealed class ProjectSettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "New Project";

    public string Description { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;

    public string? WorkspacePath { get; set; }

    public LlmProvider? Provider { get; set; }

    public string? ApiKey { get; set; }

    public string? Model { get; set; }

    public string? Endpoint { get; set; }

    public string? ThemeId { get; set; }

    public string? FontFamily { get; set; }
}
