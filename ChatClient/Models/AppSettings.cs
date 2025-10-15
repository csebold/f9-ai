using ChatClient.Services;

namespace ChatClient.Models;

public sealed class AppSettings
{
    public LlmProvider Provider { get; set; } = LlmProvider.OpenAi;

    public ProviderSettings OpenAi { get; set; } = new();

    public ProviderSettings Anthropic { get; set; } = new();

    public ProviderSettings OpenRouter { get; set; } = new();

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
