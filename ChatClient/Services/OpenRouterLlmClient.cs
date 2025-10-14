using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ChatClient.Services;

internal sealed class OpenRouterLlmClient : ILlmClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly Uri _endpoint;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string? _referer;
    private readonly string? _appTitle;
    private readonly double _temperature;

    public OpenRouterLlmClient(HttpClient httpClient, string apiKey, string model, Uri? baseUri = null, string? referer = null, string? appTitle = null, double temperature = 0.7)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? throw new ArgumentException("API key cannot be null or whitespace.", nameof(apiKey)) : apiKey;
        _model = string.IsNullOrWhiteSpace(model) ? throw new ArgumentException("Model cannot be null or whitespace.", nameof(model)) : model;
        _referer = string.IsNullOrWhiteSpace(referer) ? null : referer;
        _appTitle = string.IsNullOrWhiteSpace(appTitle) ? null : appTitle;
        _temperature = temperature;
        _endpoint = baseUri is null
            ? new Uri("https://openrouter.ai/api/v1/chat/completions")
            : new Uri(baseUri, "chat/completions");
    }

    public async Task<string> GetResponseAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        if (_referer is not null)
        {
            request.Headers.TryAddWithoutValidation("HTTP-Referer", _referer);
        }

        if (_appTitle is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Title", _appTitle);
        }

        var payload = new ChatCompletionRequest(_model, new[]
        {
            new ChatMessage("user", prompt)
        }, _temperature);

        request.Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenRouter request failed: {response.StatusCode} {responseText}");
        }

        var completion = JsonSerializer.Deserialize<ChatCompletionResponse>(responseText, SerializerOptions);
        if (completion?.Choices is null || completion.Choices.Count == 0)
        {
            throw new InvalidOperationException("OpenRouter returned an empty response.");
        }

        var assistantMessage = completion.Choices[0].Message?.Content;
        if (string.IsNullOrWhiteSpace(assistantMessage))
        {
            throw new InvalidOperationException("OpenRouter response did not include any assistant content.");
        }

        return assistantMessage.Trim();
    }

    private sealed record ChatCompletionRequest(string Model, IReadOnlyList<ChatMessage> Messages, double Temperature);

    private sealed record ChatMessage(string Role, string Content);

    private sealed record ChatCompletionResponse(IReadOnlyList<Choice> Choices);

    private sealed record Choice(ChatMessage? Message);
}
