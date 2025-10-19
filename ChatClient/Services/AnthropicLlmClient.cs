using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ChatClient.Services;

internal sealed class AnthropicLlmClient : ILlmClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly Uri _endpoint;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly int _maxTokens;

    public AnthropicLlmClient(HttpClient httpClient, string apiKey, string model, Uri? baseUri = null, int maxTokens = 1024)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? throw new ArgumentException("API key cannot be null or whitespace.", nameof(apiKey)) : apiKey;
        _model = string.IsNullOrWhiteSpace(model) ? throw new ArgumentException("Model cannot be null or whitespace.", nameof(model)) : model;
        _maxTokens = maxTokens;
        _endpoint = baseUri is null
            ? new Uri("https://api.anthropic.com/v1/messages")
            : new Uri(baseUri, "messages");
    }

    public async Task<string> GetResponseAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var prompt = request.Prompt;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt cannot be empty.", nameof(request));
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        httpRequest.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
        httpRequest.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");

        var payload = new AnthropicRequest(
            _model,
            _maxTokens,
            new[]
            {
                new AnthropicMessage("user", new[]
                {
                    new AnthropicContent("text", prompt)
                })
            },
            request.IncludeInstructions ? request.Instructions : null);

        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Anthropic request failed: {response.StatusCode} {responseText}");
        }

        var completion = JsonSerializer.Deserialize<AnthropicResponse>(responseText, SerializerOptions);
        if (completion?.Content is null || completion.Content.Count == 0)
        {
            throw new InvalidOperationException("Anthropic returned an empty response.");
        }

        foreach (var content in completion.Content)
        {
            if (content.Type == "text" && !string.IsNullOrWhiteSpace(content.Text))
            {
                return content.Text.Trim();
            }
        }

        throw new InvalidOperationException("Anthropic response did not include any textual content.");
    }

    private sealed record AnthropicRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("messages")] IReadOnlyList<AnthropicMessage> Messages,
        [property: JsonPropertyName("system")] string? SystemInstructions);

    private sealed record AnthropicMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] IReadOnlyList<AnthropicContent> Content);

    private sealed record AnthropicContent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string Text);

    private sealed record AnthropicResponse([
        property: JsonPropertyName("content")] IReadOnlyList<AnthropicContentResponse> Content);

    private sealed record AnthropicContentResponse(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string? Text);
}
