using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Models;

namespace ChatClient.Services;

public sealed class ModelCatalogService : IModelCatalogService, IDisposable
{
    private const string AnthropicVersion = "2023-06-01";
    private const string DefaultOllamaBase = "http://localhost:11434/";
    private static readonly Uri OllamaLibraryUri = new("https://ollama.com/library");
    private static readonly Regex OllamaLibraryAnchorRegex = new("<a[^>]*class=\"[^\"]*font-mono[^\"]*\"[^>]*>(?<name>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex OllamaLibraryTitleRegex = new("<(?<tag>div|span)[^>]*x-test-model-title[^>]*title=\"(?<name>[^\"]+)\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string[] KnownGoodOllamaModels =
    {
        "llama3",
        "llama3.1",
        "llama3.2",
        "llama3:70b",
        "mistral",
        "mixtral",
        "codellama",
        "codellama:34b-instruct",
        "gemma",
        "gemma:7b",
        "phi",
        "openchat",
        "dolphin",
        "tinyllama",
        "orca-mini",
        "llava"
    };

    private readonly HttpClient _httpClient;
    private bool _disposed;

    public ModelCatalogService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<IReadOnlyList<ModelCatalogEntry>> GetModelsAsync(
        LlmProvider provider,
        ProviderSettings providerSettings,
        CancellationToken cancellationToken)
    {
        if (providerSettings is null)
        {
            throw new ArgumentNullException(nameof(providerSettings));
        }

        return provider switch
        {
            LlmProvider.Anthropic => await MapToEntriesAsync(
                () => GetAnthropicModelsAsync(RequireApiKey(providerSettings.ApiKey), cancellationToken)).ConfigureAwait(false),
            LlmProvider.OpenRouter => await MapToEntriesAsync(
                () => GetOpenRouterModelsAsync(RequireApiKey(providerSettings.ApiKey), cancellationToken)).ConfigureAwait(false),
            LlmProvider.Ollama => await GetOllamaModelsAsync(providerSettings.Endpoint, cancellationToken).ConfigureAwait(false),
            _ => await MapToEntriesAsync(
                () => GetOpenAiModelsAsync(RequireApiKey(providerSettings.ApiKey), cancellationToken)).ConfigureAwait(false),
        };
    }

    public async Task DownloadModelAsync(
        LlmProvider provider,
        ProviderSettings providerSettings,
        string modelId,
        CancellationToken cancellationToken)
    {
        if (providerSettings is null)
        {
            throw new ArgumentNullException(nameof(providerSettings));
        }

        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new ArgumentException("Model identifier must be provided.", nameof(modelId));
        }

        switch (provider)
        {
            case LlmProvider.Ollama:
                await DownloadOllamaModelAsync(providerSettings, modelId.Trim(), cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new NotSupportedException("Downloading models is only supported for Ollama.");
        }
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

    private async Task<IReadOnlyList<ModelCatalogEntry>> GetOllamaModelsAsync(string? endpoint, CancellationToken cancellationToken)
    {
        var installed = await GetInstalledOllamaModelsAsync(endpoint, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<OllamaLibraryModel> library;
        try
        {
            library = await GetOllamaLibraryModelsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            library = Array.Empty<OllamaLibraryModel>();
        }

        var entries = new Dictionary<string, ModelCatalogEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var model in installed)
        {
            entries[model] = new ModelCatalogEntry(
                model,
                true,
                IsDownloadableFromLibrary(model, library),
                IsRecommended(model, library));
        }

        foreach (var model in library)
        {
            var entry = new ModelCatalogEntry(
                model.Name,
                entries.ContainsKey(model.Name),
                true,
                model.IsRecommended);
            if (entries.TryGetValue(model.Name, out var existing))
            {
                entries[model.Name] = existing.Merge(entry);
            }
            else
            {
                entries[model.Name] = entry;
            }
        }

        return entries.Values
            .OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<IReadOnlyList<string>> GetInstalledOllamaModelsAsync(string? endpoint, CancellationToken cancellationToken)
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

    private async Task<IReadOnlyList<OllamaLibraryModel>> GetOllamaLibraryModelsAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, OllamaLibraryUri);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        var contentBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, contentBytes);

        var html = Encoding.UTF8.GetString(contentBytes);
        return ParseOllamaLibrary(html);
    }

    private static IReadOnlyList<ModelCatalogEntry> MapToEntries(IReadOnlyList<string> models)
    {
        if (models.Count == 0)
        {
            return Array.Empty<ModelCatalogEntry>();
        }

        var result = new List<ModelCatalogEntry>(models.Count);
        foreach (var model in models)
        {
            result.Add(new ModelCatalogEntry(model, true, false, false));
        }

        return result;
    }

    private static async Task<IReadOnlyList<ModelCatalogEntry>> MapToEntriesAsync(Func<Task<IReadOnlyList<string>>> producer)
    {
        var models = await producer().ConfigureAwait(false);
        return MapToEntries(models);
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

    private static IReadOnlyList<OllamaLibraryModel> ParseOllamaLibrary(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return Array.Empty<OllamaLibraryModel>();
        }

        var builder = new List<OllamaLibraryModel>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddEntry(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            var decoded = WebUtility.HtmlDecode(raw);
            var stripped = StripTags(decoded).Trim();
            if (string.IsNullOrWhiteSpace(stripped))
            {
                return;
            }

            if (!seen.Add(stripped))
            {
                return;
            }

            var isRecommended = KnownGoodOllamaModels.Any(g =>
                stripped.Contains(g, StringComparison.OrdinalIgnoreCase));

            builder.Add(new OllamaLibraryModel(stripped, isRecommended));
        }

        foreach (Match match in OllamaLibraryTitleRegex.Matches(html))
        {
            AddEntry(match.Groups["name"].Value);
        }

        if (builder.Count == 0)
        {
            foreach (Match match in OllamaLibraryAnchorRegex.Matches(html))
            {
                AddEntry(match.Groups["name"].Value);
            }
        }

        return builder;
    }

    private static bool IsRecommended(string modelName, IReadOnlyList<OllamaLibraryModel> library)
    {
        foreach (var entry in library)
        {
            if (string.Equals(entry.Name, modelName, StringComparison.OrdinalIgnoreCase))
            {
                return entry.IsRecommended;
            }
        }

        return KnownGoodOllamaModels.Any(g =>
            modelName.Contains(g, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDownloadableFromLibrary(string modelName, IReadOnlyList<OllamaLibraryModel> library) =>
        library.Any(entry => string.Equals(entry.Name, modelName, StringComparison.OrdinalIgnoreCase));

    private static string StripTags(string value) => Regex.Replace(value, "<.*?>", string.Empty).Trim();

    private static void EnsureSuccess(HttpResponseMessage response, ReadOnlyMemory<byte> payload)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = payload.Length > 0
            ? Encoding.UTF8.GetString(payload.Span)
            : response.ReasonPhrase ?? "Unknown error";

        throw new InvalidOperationException($"Request failed ({response.StatusCode}): {message}");
    }

    private static async Task DownloadOllamaModelAsync(ProviderSettings providerSettings, string modelId, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ollama",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("pull");
        startInfo.ArgumentList.Add(modelId);

        var endpoint = providerSettings.Endpoint;
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            if (!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var endpointUri))
            {
                throw new InvalidOperationException($"Invalid Ollama endpoint '{endpoint}'.");
            }

            startInfo.Environment["OLLAMA_HOST"] = endpointUri.Authority;
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Failed to start the Ollama CLI. Ensure 'ollama' is installed and on the PATH.");
        }

        await using var _ = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore kill failures.
            }
        });

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            var messageBuilder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(stdoutTask.Result))
            {
                messageBuilder.AppendLine(stdoutTask.Result.Trim());
            }

            if (!string.IsNullOrWhiteSpace(stderrTask.Result))
            {
                messageBuilder.AppendLine(stderrTask.Result.Trim());
            }

            var errorMessage = messageBuilder.Length == 0
                ? $"ollama pull failed with exit code {process.ExitCode}."
                : messageBuilder.ToString().Trim();

            throw new InvalidOperationException(errorMessage);
        }
    }

    private readonly record struct OllamaLibraryModel(string Name, bool IsRecommended);
}
