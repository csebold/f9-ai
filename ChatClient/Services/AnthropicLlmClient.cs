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

    public async Task<string> GetResponseAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        request.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
        request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");

        var payload = new AnthropicRequest(
            _model,
            _maxTokens,
            new[]
            {
                new AnthropicMessage("user", new[]
                {
                    new AnthropicContent("text", prompt)
                })
            });

        request.Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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

    private sealed record AnthropicRequest(string Model, int MaxTokens, IReadOnlyList<AnthropicMessage> Messages);

    private sealed record AnthropicMessage(string Role, IReadOnlyList<AnthropicContent> Content);

    private sealed record AnthropicContent(string Type, string Text);

    private sealed record AnthropicResponse(IReadOnlyList<AnthropicContentResponse> Content);

    private sealed record AnthropicContentResponse(string Type, string? Text);
}
