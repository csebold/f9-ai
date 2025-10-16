using System.Collections.Generic;
using ChatClient.Services;

namespace ChatClient.Models;

public sealed class AppSettings
{
    public LlmProvider Provider { get; set; } = LlmProvider.OpenAi;

    public ProviderSettings OpenAi { get; set; } = new();

    public ProviderSettings Anthropic { get; set; } = new();

    public ProviderSettings OpenRouter { get; set; } = new();

    public string? ActiveProjectId { get; set; }

    public List<ProjectSettings> Projects { get; set; } = new();

    public bool EnableSessionPersistence { get; set; } = true;

    public int MaxSessionsPerProject { get; set; } = 10;

    public int MaxMessagesPerSession { get; set; } = 200;

    public Dictionary<string, string> ActiveSessions { get; set; } = new();

    public ProviderSettings GetProviderSettings(LlmProvider provider) =>
        provider switch
        {
            LlmProvider.OpenAi => OpenAi,
            LlmProvider.Anthropic => Anthropic,
            LlmProvider.OpenRouter => OpenRouter,
            _ => OpenAi
        };
}

public sealed class ProviderSettings
{
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;
}
