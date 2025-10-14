using System;
using System.Net.Http;

namespace ChatClient.Services;

public static class LlmClientFactory
{
    private const string DefaultOpenAiBase = "https://api.openai.com/v1/";
    private const string DefaultAnthropicBase = "https://api.anthropic.com/v1/";
    private const string DefaultOpenRouterBase = "https://openrouter.ai/api/v1/";

    public static ILlmClient CreateDefault()
    {
        var providerValue = Environment.GetEnvironmentVariable("LLM_PROVIDER");
        var provider = ParseProvider(providerValue);

        return provider switch
        {
            LlmProvider.Anthropic => CreateAnthropicClient(),
            LlmProvider.OpenRouter => CreateOpenRouterClient(),
            _ => CreateOpenAiClient(),
        };
    }

    private static LlmProvider ParseProvider(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return LlmProvider.OpenAi;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "anthropic" => LlmProvider.Anthropic,
            "openrouter" => LlmProvider.OpenRouter,
            "openai" => LlmProvider.OpenAi,
            _ => throw new InvalidOperationException($"Unsupported LLM provider '{value}'. Expected 'openai', 'anthropic', or 'openrouter'.")
        };
    }

    private static ILlmClient CreateOpenAiClient()
    {
        var apiKey = GetRequiredEnvironmentVariable("OPENAI_API_KEY");
        var model = GetOptionalEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";
        var baseUrl = GetOptionalEnvironmentVariable("OPENAI_BASE_URL") ?? DefaultOpenAiBase;

        var httpClient = CreateHttpClient(baseUrl);
        return new OpenAiLlmClient(httpClient, apiKey, model, httpClient.BaseAddress);
    }

    private static ILlmClient CreateAnthropicClient()
    {
        var apiKey = GetRequiredEnvironmentVariable("ANTHROPIC_API_KEY");
        var model = GetOptionalEnvironmentVariable("ANTHROPIC_MODEL") ?? "claude-3-haiku-20240307";
        var maxTokens = ParseOptionalInt("ANTHROPIC_MAX_TOKENS") ?? 1024;
        var baseUrl = GetOptionalEnvironmentVariable("ANTHROPIC_BASE_URL") ?? DefaultAnthropicBase;

        var httpClient = CreateHttpClient(baseUrl);
        return new AnthropicLlmClient(httpClient, apiKey, model, httpClient.BaseAddress, maxTokens);
    }

    private static ILlmClient CreateOpenRouterClient()
    {
        var apiKey = GetRequiredEnvironmentVariable("OPENROUTER_API_KEY");
        var model = GetOptionalEnvironmentVariable("OPENROUTER_MODEL") ?? "openrouter/auto";
        var baseUrl = GetOptionalEnvironmentVariable("OPENROUTER_BASE_URL") ?? DefaultOpenRouterBase;
        var referer = GetOptionalEnvironmentVariable("OPENROUTER_HTTP_REFERER");
        var title = GetOptionalEnvironmentVariable("OPENROUTER_APP_TITLE");

        var httpClient = CreateHttpClient(baseUrl);
        return new OpenRouterLlmClient(httpClient, apiKey, model, httpClient.BaseAddress, referer, title);
    }

    private static HttpClient CreateHttpClient(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Invalid base URL '{baseUrl}'.");
        }

        return new HttpClient
        {
            BaseAddress = uri,
            Timeout = TimeSpan.FromSeconds(100)
        };
    }

    private static string? GetOptionalEnvironmentVariable(string name)
        => Environment.GetEnvironmentVariable(name);

    private static string GetRequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Environment variable '{name}' must be set for the selected LLM provider.");
        }

        return value;
    }

    private static int? ParseOptionalInt(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Environment variable '{name}' must be a valid integer.");
    }
}
