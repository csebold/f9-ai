using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Models;

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

        var messages = new List<ChatMessage>();
        if (request.IncludeInstructions && !string.IsNullOrWhiteSpace(request.Instructions))
        {
            messages.Add(new ChatMessage("system", request.Instructions!));
        }

        var includesCurrentPrompt = false;

        if (request.History.Count > 0)
        {
            foreach (var historyMessage in request.History)
            {
                var role = historyMessage.Role switch
                {
                    MessageRole.User => "user",
                    MessageRole.Assistant => "assistant",
                    MessageRole.System => "system",
                    _ => "user"
                };

                messages.Add(new ChatMessage(role, historyMessage.Content));

                if (!includesCurrentPrompt
                    && historyMessage.Role == MessageRole.User
                    && string.Equals(historyMessage.Content, prompt, StringComparison.Ordinal))
                {
                    includesCurrentPrompt = true;
                }
            }
        }

        if (!includesCurrentPrompt)
        {
            messages.Add(new ChatMessage("user", prompt));
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        if (_referer is not null)
        {
            httpRequest.Headers.TryAddWithoutValidation("HTTP-Referer", _referer);
        }

        if (_appTitle is not null)
        {
            httpRequest.Headers.TryAddWithoutValidation("X-Title", _appTitle);
        }

        var payload = new ChatCompletionRequest(_model, messages, _temperature);
        var payloadJson = JsonSerializer.Serialize(payload, SerializerOptions);
        httpRequest.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

        var metadata = new List<string>
        {
            $"Model: {_model}",
            $"Temperature: {_temperature}"
        };

        if (!string.IsNullOrWhiteSpace(_referer))
        {
            metadata.Add($"Referer: {_referer}");
        }

        if (!string.IsNullOrWhiteSpace(_appTitle))
        {
            metadata.Add($"App Title: {_appTitle}");
        }

        try
        {
            await ChatLogScope.LogAsync("SEND", $"OpenRouter POST {_endpoint}", metadata, payloadJson).ConfigureAwait(false);

            using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var statusText = $"{(int)response.StatusCode} {response.StatusCode}";
            await ChatLogScope.LogAsync("RECEIVE", $"OpenRouter HTTP {statusText}", metadata, responseText).ConfigureAwait(false);

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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await ChatLogScope.LogAsync(
                "ERROR",
                "OpenRouter request failed",
                new[] { ex.GetType().Name },
                ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    private sealed record ChatCompletionRequest(string Model, IReadOnlyList<ChatMessage> Messages, double Temperature);

    private sealed record ChatMessage(string Role, string Content);

    private sealed record ChatCompletionResponse(IReadOnlyList<Choice> Choices);

    private sealed record Choice(ChatMessage? Message);
}
