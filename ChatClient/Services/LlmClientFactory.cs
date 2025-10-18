using System;
using System.Net.Http;
using ChatClient.Models;

namespace ChatClient.Services;

public static class LlmClientFactory
{
    private const string DefaultOpenAiBase = "https://api.openai.com/v1/";
    private const string DefaultAnthropicBase = "https://api.anthropic.com/v1/";
    private const string DefaultOpenRouterBase = "https://openrouter.ai/api/v1/";
    private const string DefaultOllamaBase = "http://localhost:11434/";

    public static LlmClientRegistration CreateDefault()
    {
        var providerValue = Environment.GetEnvironmentVariable("LLM_PROVIDER");
        var provider = ParseProvider(providerValue);

        return provider switch
        {
            LlmProvider.Anthropic => CreateAnthropicClient(),
            LlmProvider.OpenRouter => CreateOpenRouterClient(),
            LlmProvider.Ollama => CreateOllamaClient(),
            _ => CreateOpenAiClient(),
        };
    }

    public static LlmClientRegistration CreateFromSettings(AppSettings settings) =>
        CreateForProject(settings, project: null);

    public static LlmClientRegistration CreateForProject(AppSettings settings, ProjectSettings? project)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        var provider = ResolveProvider(settings, project);
        var providerSettings = ResolveProviderSettings(settings, project, provider);

        return provider switch
        {
            LlmProvider.Anthropic => CreateAnthropicClient(providerSettings),
            LlmProvider.OpenRouter => CreateOpenRouterClient(providerSettings),
            LlmProvider.Ollama => CreateOllamaClient(providerSettings),
            _ => CreateOpenAiClient(providerSettings),
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
            "ollama" => LlmProvider.Ollama,
            _ => throw new InvalidOperationException($"Unsupported LLM provider '{value}'. Expected 'openai', 'anthropic', 'openrouter', or 'ollama'.")
        };
    }

    private static LlmClientRegistration CreateOpenAiClient()
    {
        var apiKey = GetRequiredEnvironmentVariable("OPENAI_API_KEY");
        var model = GetOptionalEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini";
        var baseUrl = GetOptionalEnvironmentVariable("OPENAI_BASE_URL") ?? DefaultOpenAiBase;

        var httpClient = CreateHttpClient(baseUrl);
        var client = new OpenAiLlmClient(httpClient, apiKey, model, httpClient.BaseAddress);
        return new LlmClientRegistration(client, LlmProvider.OpenAi, "OpenAI", model, $"Connected to OpenAI ({model}).")
        {
            Endpoint = httpClient.BaseAddress
        };
    }

    private static LlmClientRegistration CreateAnthropicClient()
    {
        var apiKey = GetRequiredEnvironmentVariable("ANTHROPIC_API_KEY");
        var model = GetOptionalEnvironmentVariable("ANTHROPIC_MODEL") ?? "claude-3-haiku-20240307";
        var maxTokens = ParseOptionalInt("ANTHROPIC_MAX_TOKENS") ?? 1024;
        var baseUrl = GetOptionalEnvironmentVariable("ANTHROPIC_BASE_URL") ?? DefaultAnthropicBase;

        var httpClient = CreateHttpClient(baseUrl);
        var client = new AnthropicLlmClient(httpClient, apiKey, model, httpClient.BaseAddress, maxTokens);
        return new LlmClientRegistration(client, LlmProvider.Anthropic, "Anthropic", model, $"Connected to Anthropic ({model}).")
        {
            Endpoint = httpClient.BaseAddress
        };
    }

    private static LlmClientRegistration CreateOpenRouterClient()
    {
        var apiKey = GetRequiredEnvironmentVariable("OPENROUTER_API_KEY");
        var model = GetOptionalEnvironmentVariable("OPENROUTER_MODEL") ?? "openrouter/auto";
        var baseUrl = GetOptionalEnvironmentVariable("OPENROUTER_BASE_URL") ?? DefaultOpenRouterBase;
        var referer = GetOptionalEnvironmentVariable("OPENROUTER_HTTP_REFERER");
        var title = GetOptionalEnvironmentVariable("OPENROUTER_APP_TITLE");

        var httpClient = CreateHttpClient(baseUrl);
        var client = new OpenRouterLlmClient(httpClient, apiKey, model, httpClient.BaseAddress, referer, title);
        return new LlmClientRegistration(client, LlmProvider.OpenRouter, "OpenRouter", model, $"Connected to OpenRouter ({model}).")
        {
            Endpoint = httpClient.BaseAddress
        };
    }

    private static LlmClientRegistration CreateOllamaClient()
    {
        var baseUrl = GetOptionalEnvironmentVariable("OLLAMA_ENDPOINT") ?? DefaultOllamaBase;
        var model = GetOptionalEnvironmentVariable("OLLAMA_MODEL") ?? "llama3";

        var httpClient = CreateHttpClient(baseUrl);
        var client = new OllamaLlmClient(httpClient, model, httpClient.BaseAddress);
        return new LlmClientRegistration(client, LlmProvider.Ollama, "Ollama", model, $"Connected to Ollama ({model}).")
        {
            Endpoint = httpClient.BaseAddress
        };
    }

    private static LlmClientRegistration CreateOpenAiClient(ProviderSettings settings)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        var apiKey = RequireValue(settings.ApiKey, "OpenAI API key");
        var model = string.IsNullOrWhiteSpace(settings.Model) ? "gpt-4o-mini" : settings.Model.Trim();
        var httpClient = CreateHttpClient(DefaultOpenAiBase);
        var client = new OpenAiLlmClient(httpClient, apiKey, model, httpClient.BaseAddress);
        return new LlmClientRegistration(client, LlmProvider.OpenAi, "OpenAI", model, $"Connected to OpenAI ({model}).")
        {
            Endpoint = httpClient.BaseAddress
        };
    }

    private static LlmClientRegistration CreateAnthropicClient(ProviderSettings settings)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        var apiKey = RequireValue(settings.ApiKey, "Anthropic API key");
        var model = string.IsNullOrWhiteSpace(settings.Model) ? "claude-3-haiku-20240307" : settings.Model.Trim();
        var httpClient = CreateHttpClient(DefaultAnthropicBase);
        var client = new AnthropicLlmClient(httpClient, apiKey, model, httpClient.BaseAddress, 1024);
        return new LlmClientRegistration(client, LlmProvider.Anthropic, "Anthropic", model, $"Connected to Anthropic ({model}).")
        {
            Endpoint = httpClient.BaseAddress
        };
    }

    private static LlmClientRegistration CreateOpenRouterClient(ProviderSettings settings)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        var apiKey = RequireValue(settings.ApiKey, "OpenRouter API key");
        var model = string.IsNullOrWhiteSpace(settings.Model) ? "openrouter/auto" : settings.Model.Trim();
        var httpClient = CreateHttpClient(DefaultOpenRouterBase);
        var client = new OpenRouterLlmClient(httpClient, apiKey, model, httpClient.BaseAddress, null, null);
        return new LlmClientRegistration(client, LlmProvider.OpenRouter, "OpenRouter", model, $"Connected to OpenRouter ({model}).")
        {
            Endpoint = httpClient.BaseAddress
        };
    }

    private static LlmClientRegistration CreateOllamaClient(ProviderSettings settings)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        var baseUrl = string.IsNullOrWhiteSpace(settings.Endpoint)
            ? DefaultOllamaBase
            : settings.Endpoint.Trim();
        var model = string.IsNullOrWhiteSpace(settings.Model) ? "llama3" : settings.Model.Trim();

        var httpClient = CreateHttpClient(baseUrl);
        var client = new OllamaLlmClient(httpClient, model, httpClient.BaseAddress);
        var status = httpClient.BaseAddress is null
            ? $"Connected to Ollama ({model})."
            : $"Connected to Ollama ({model}) at {httpClient.BaseAddress}";
        return new LlmClientRegistration(client, LlmProvider.Ollama, "Ollama", model, status)
        {
            Endpoint = httpClient.BaseAddress
        };
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

    private static string RequireValue(string value, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{description} must be configured.");
        }

        return value.Trim();
    }

    private static LlmProvider ResolveProvider(AppSettings settings, ProjectSettings? project)
    {
        if (project?.Provider is { } explicitProvider)
        {
            return explicitProvider;
        }

        if (project is not null)
        {
            if (!string.IsNullOrWhiteSpace(project.Endpoint))
            {
                if (InferProviderFromEndpoint(project.Endpoint) is { } inferredFromEndpoint)
                {
                    return inferredFromEndpoint;
                }
            }

            if (!string.IsNullOrWhiteSpace(project.Model))
            {
                if (InferProviderFromModel(project.Model) is { } inferredFromModel)
                {
                    return inferredFromModel;
                }
            }
        }

        return settings.Provider;
    }

    private static LlmProvider? InferProviderFromEndpoint(string endpoint)
    {
        if (endpoint.Contains("11434", StringComparison.Ordinal))
        {
            return LlmProvider.Ollama;
        }

        return null;
    }

    private static LlmProvider? InferProviderFromModel(string model)
    {
        if (model.Contains(':', StringComparison.Ordinal) && !model.Contains('/', StringComparison.Ordinal))
        {
            // Models with colon notation and no slash are typically Ollama tags (e.g., "llama3:latest").
            return LlmProvider.Ollama;
        }

        return null;
    }

    private static ProviderSettings ResolveProviderSettings(AppSettings settings, ProjectSettings? project, LlmProvider provider)
    {
        var baseSettings = settings.GetProviderSettings(provider);
        var apiKey = baseSettings.ApiKey;
        var model = baseSettings.Model;
        var endpoint = baseSettings.Endpoint;

        if (project is not null && (project.Provider is null || project.Provider == provider))
        {
            if (!string.IsNullOrWhiteSpace(project.ApiKey))
            {
                apiKey = project.ApiKey.Trim();
            }

            if (!string.IsNullOrWhiteSpace(project.Model))
            {
                model = project.Model.Trim();
            }

            if (!string.IsNullOrWhiteSpace(project.Endpoint))
            {
                endpoint = project.Endpoint.Trim();
            }
        }

        return new ProviderSettings
        {
            ApiKey = apiKey,
            Model = model,
            Endpoint = endpoint
        };
    }
}
