using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ChatClient.Services;

public sealed class ModelCatalogService : IModelCatalogService, IDisposable
{
    private const string AnthropicVersion = "2023-06-01";

    private readonly HttpClient _httpClient;
    private bool _disposed;

    public ModelCatalogService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<IReadOnlyList<string>> GetModelsAsync(LlmProvider provider, string apiKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("An API key is required to load models for the selected provider.");
        }

        return provider switch
        {
            LlmProvider.Anthropic => await GetAnthropicModelsAsync(apiKey, cancellationToken).ConfigureAwait(false),
            LlmProvider.OpenRouter => await GetOpenRouterModelsAsync(apiKey, cancellationToken).ConfigureAwait(false),
            _ => await GetOpenAiModelsAsync(apiKey, cancellationToken).ConfigureAwait(false),
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
