using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ChatClient.Services;

internal sealed class OllamaLlmClient : ILlmClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly Uri _endpoint;
    private readonly string _model;

    public OllamaLlmClient(HttpClient httpClient, string model, Uri? baseUri = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _model = string.IsNullOrWhiteSpace(model)
            ? throw new ArgumentException("Model cannot be null or whitespace.", nameof(model))
            : model.Trim();
        _endpoint = baseUri is null
            ? new Uri("http://localhost:11434/api/chat", UriKind.Absolute)
            : new Uri(baseUri, "api/chat");
    }

    public async Task<string> GetResponseAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt cannot be empty.", nameof(prompt));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);

        var payload = new ChatRequest(_model, new[]
        {
            new ChatMessage("user", prompt)
        }, Stream: false);

        request.Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Ollama request failed: {response.StatusCode} {responseText}");
        }

        var completion = JsonSerializer.Deserialize<ChatResponse>(responseText, SerializerOptions);
        var assistantMessage = completion?.Message?.Content;
        if (string.IsNullOrWhiteSpace(assistantMessage))
        {
            throw new InvalidOperationException("Ollama response did not include any assistant content.");
        }

        return assistantMessage.Trim();
    }

    private sealed record ChatRequest(string Model, IReadOnlyList<ChatMessage> Messages, bool Stream);

    private sealed record ChatMessage(string Role, string Content);

    private sealed record ChatResponse(ChatResponseMessage? Message);

    private sealed record ChatResponseMessage(string? Role, string? Content);
}
