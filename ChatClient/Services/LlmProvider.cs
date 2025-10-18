namespace ChatClient.Services;

/// <summary>
/// Supported LLM backends.
/// </summary>
public enum LlmProvider
{
    OpenAi,
    Anthropic,
    OpenRouter,
    Ollama
}
