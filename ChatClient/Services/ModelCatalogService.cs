using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Models;

namespace ChatClient.Services;

public sealed class ModelCatalogService : IModelCatalogService, IDisposable
{
    private const string AnthropicVersion = "2023-06-01";
    private const string DefaultOllamaBase = "http://localhost:11434/";

    private readonly HttpClient _httpClient;
    private bool _disposed;

    public ModelCatalogService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<IReadOnlyList<string>> GetModelsAsync(LlmProvider provider, ProviderSettings providerSettings, CancellationToken cancellationToken)
    {
        if (providerSettings is null)
        {
            throw new ArgumentNullException(nameof(providerSettings));
        }

        return provider switch
        {
            LlmProvider.Anthropic => await GetAnthropicModelsAsync(RequireApiKey(providerSettings.ApiKey), cancellationToken).ConfigureAwait(false),
            LlmProvider.OpenRouter => await GetOpenRouterModelsAsync(RequireApiKey(providerSettings.ApiKey), cancellationToken).ConfigureAwait(false),
            LlmProvider.Ollama => await GetOllamaModelsAsync(providerSettings.Endpoint, cancellationToken).ConfigureAwait(false),
            _ => await GetOpenAiModelsAsync(RequireApiKey(providerSettings.ApiKey), cancellationToken).ConfigureAwait(false),
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _httpClient.Dispose();
        _disposed = true;
    }

    private static string RequireApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("An API key is required to load models for the selected provider.");
        }

        return apiKey.Trim();
    }

    private async Task<IReadOnlyList<string>> GetOpenAiModelsAsync(string apiKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, content);

        using var document = JsonDocument.Parse(content);
        var models = ExtractModelIds(document.RootElement, "data");
        return SortAndDistinct(models);
    }

    private async Task<IReadOnlyList<string>> GetAnthropicModelsAsync(string apiKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/models");
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        request.Headers.TryAddWithoutValidation("anthropic-version", AnthropicVersion);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, content);

        using var document = JsonDocument.Parse(content);
        var models = ExtractModelIds(document.RootElement, "models", "data");
        return SortAndDistinct(models);
    }

    private async Task<IReadOnlyList<string>> GetOpenRouterModelsAsync(string apiKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://openrouter.ai/api/v1/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, content);

        using var document = JsonDocument.Parse(content);
        var models = ExtractModelIds(document.RootElement, "data", "models");
        return SortAndDistinct(models);
    }

    private async Task<IReadOnlyList<string>> GetOllamaModelsAsync(string? endpoint, CancellationToken cancellationToken)
    {
        var baseUrl = string.IsNullOrWhiteSpace(endpoint) ? DefaultOllamaBase : endpoint.Trim();
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException($"Invalid Ollama endpoint '{endpoint}'.");
        }

        var tagsUri = new Uri(baseUri, "api/tags");

        using var request = new HttpRequestMessage(HttpMethod.Get, tagsUri);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, content);

        using var document = JsonDocument.Parse(content);
        var models = ExtractOllamaModelNames(document.RootElement);
        return SortAndDistinct(models);
    }

    private static IReadOnlyList<string> ExtractModelIds(JsonElement root, params string[] arrayPropertyCandidates)
    {
        foreach (var property in arrayPropertyCandidates)
        {
            if (root.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array)
            {
                var builder = new List<string>();
                foreach (var item in array.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("id", out var idElement))
                    {
                        var id = idElement.GetString();
                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            builder.Add(id);
                        }
                    }
                }

                if (builder.Count > 0)
                {
                    return builder;
                }
            }
        }

        return Array.Empty<string>();
    }

    private static IReadOnlyList<string> ExtractOllamaModelNames(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<string>();
        }

        if (!root.TryGetProperty("models", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var builder = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("name", out var nameElement))
            {
                var name = nameElement.GetString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    builder.Add(name);
                }
            }
        }

        return builder;
    }

    private static IReadOnlyList<string> SortAndDistinct(IReadOnlyList<string> models)
    {
        if (models.Count == 0)
        {
            return models;
        }

        return models
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void EnsureSuccess(HttpResponseMessage response, ReadOnlyMemory<byte> payload)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = payload.Length > 0
            ? System.Text.Encoding.UTF8.GetString(payload.Span)
            : response.ReasonPhrase ?? "Unknown error";

        throw new InvalidOperationException($"Request failed ({response.StatusCode}): {message}");
    }
}
