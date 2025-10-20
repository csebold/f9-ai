using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Models;
using ChatClient.Services;
using Xunit;

namespace ChatClient.Tests.Services;

public class LlmClientTests
{
    [Fact]
    public async Task OpenAiClient_SendsExpectedPayload()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"choices":[{"message":{"content":" hello "}}]}
                """)
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.com/v1/")
        };

        var client = new OpenAiLlmClient(httpClient, "api-key", "gpt-test", httpClient.BaseAddress, 0.5);

        var response = await client.GetResponseAsync(new LlmRequest("prompt text"));

        Assert.Equal("hello", response);

        var request = handler.LastRequest!;
        Assert.Equal(new Uri("https://example.com/v1/chat/completions"), request.RequestUri);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("api-key", request.Headers.Authorization?.Parameter);

        using var document = JsonDocument.Parse(handler.LastRequestContent!);
        var root = document.RootElement;
        Assert.Equal("gpt-test", root.GetProperty("model").GetString());
        Assert.Equal(0.5, root.GetProperty("temperature").GetDouble());
        var messages = root.GetProperty("messages");
        Assert.Equal(1, messages.GetArrayLength());
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.Equal("prompt text", messages[0].GetProperty("content").GetString());
    }

    [Fact]
    public async Task OpenAiClient_IncludesSystemInstructionsWhenRequested()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"choices":[{"message":{"content":" ack "}}]}
                """)
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.com/v1/")
        };

        var client = new OpenAiLlmClient(httpClient, "api-key", "gpt-test", httpClient.BaseAddress, 0.5);

        var request = new LlmRequest("prompt text", "Follow these instructions.", includeInstructions: true);
        _ = await client.GetResponseAsync(request);

        using var document = JsonDocument.Parse(handler.LastRequestContent!);
        var messages = document.RootElement.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("Follow these instructions.", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
    }

    [Fact]
    public async Task OpenAiClient_IncludesConversationHistory()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"choices":[{"message":{"content":"ok"}}]}
                """)
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.com/v1/")
        };

        var client = new OpenAiLlmClient(httpClient, "api-key", "gpt-test", httpClient.BaseAddress, 0.5);

        var history = new[]
        {
            new LlmMessage(MessageRole.User, "Hello"),
            new LlmMessage(MessageRole.Assistant, "Hi there"),
            new LlmMessage(MessageRole.User, "How are you?")
        };

        _ = await client.GetResponseAsync(new LlmRequest("What can you do?", history: history));

        using var document = JsonDocument.Parse(handler.LastRequestContent!);
        var messages = document.RootElement.GetProperty("messages");
        Assert.Equal(4, messages.GetArrayLength());
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.Equal("Hello", messages[0].GetProperty("content").GetString());
        Assert.Equal("assistant", messages[1].GetProperty("role").GetString());
        Assert.Equal("Hi there", messages[1].GetProperty("content").GetString());
        Assert.Equal("user", messages[2].GetProperty("role").GetString());
        Assert.Equal("How are you?", messages[2].GetProperty("content").GetString());
        Assert.Equal("user", messages[3].GetProperty("role").GetString());
        Assert.Equal("What can you do?", messages[3].GetProperty("content").GetString());
    }

    [Fact]
    public async Task OpenAiClient_ThrowsOnFailureStatus()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"bad\"}")
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.com/v1/")
        };

        var client = new OpenAiLlmClient(httpClient, "api-key", "gpt-test", httpClient.BaseAddress);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetResponseAsync(new LlmRequest("prompt")));
    }

    [Fact]
    public async Task AnthropicClient_SendsMessagesAndParsesContent()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"content":[{"type":"text","text":"  reply "}]}
                """)
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://anthropic.local/v1/")
        };

        var client = new AnthropicLlmClient(httpClient, "anthropic-key", "claude", httpClient.BaseAddress, 1234);
        var result = await client.GetResponseAsync(new LlmRequest("what time is it?"));

        Assert.Equal("reply", result);

        var request = handler.LastRequest!;
        Assert.Equal("anthropic-key", request.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", request.Headers.GetValues("anthropic-version").Single());

        using var document = JsonDocument.Parse(handler.LastRequestContent!);
        var root = document.RootElement;
        Assert.Equal("claude", root.GetProperty("model").GetString());
        Assert.Equal(1234, root.GetProperty("max_tokens").GetInt32());
        var anthropicMessages = root.GetProperty("messages");
        Assert.Equal("user", anthropicMessages[0].GetProperty("role").GetString());
        Assert.Equal("what time is it?", anthropicMessages[0].GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task AnthropicClient_IncludesSystemInstructionsWhenRequested()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"content":[{"type":"text","text":"done"}]}
                """)
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://anthropic.local/v1/")
        };

        var client = new AnthropicLlmClient(httpClient, "anthropic-key", "claude", httpClient.BaseAddress, 2048);
        var request = new LlmRequest("Prompt", "Be concise.", includeInstructions: true);

        _ = await client.GetResponseAsync(request);

        using var document = JsonDocument.Parse(handler.LastRequestContent!);
        var root = document.RootElement;
        Assert.Equal("Be concise.", root.GetProperty("system").GetString());
    }

    [Fact]
    public async Task AnthropicClient_IncludesConversationHistory()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"content":[{"type":"text","text":"ok"}]}
                """)
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://anthropic.local/v1/")
        };

        var client = new AnthropicLlmClient(httpClient, "anthropic-key", "claude", httpClient.BaseAddress, 512);

        var history = new[]
        {
            new LlmMessage(MessageRole.User, "Hello"),
            new LlmMessage(MessageRole.Assistant, "Hi there"),
            new LlmMessage(MessageRole.User, "How are you?")
        };

        _ = await client.GetResponseAsync(new LlmRequest("What can you do?", history: history));

        using var document = JsonDocument.Parse(handler.LastRequestContent!);
        var messages = document.RootElement.GetProperty("messages");
        Assert.Equal(4, messages.GetArrayLength());
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.Equal("Hello", messages[0].GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal("assistant", messages[1].GetProperty("role").GetString());
        Assert.Equal("Hi there", messages[1].GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal("user", messages[2].GetProperty("role").GetString());
        Assert.Equal("How are you?", messages[2].GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Equal("user", messages[3].GetProperty("role").GetString());
        Assert.Equal("What can you do?", messages[3].GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task AnthropicClient_ThrowsWhenNoTextContent()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"content":[{"type":"image","text":null}]}
                """)
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://anthropic.local/v1/")
        };

        var client = new AnthropicLlmClient(httpClient, "anthropic-key", "claude", httpClient.BaseAddress);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetResponseAsync(new LlmRequest("prompt")));
    }

    [Fact]
    public async Task OpenRouterClient_SendsHeadersWhenConfigured()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"choices":[{"message":{"content":" ok "}}]}
                """)
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openrouter.local/api/v1/")
        };

        var client = new OpenRouterLlmClient(httpClient, "router-key", "router-model", httpClient.BaseAddress, "https://app.local", "Foundry");
        var result = await client.GetResponseAsync(new LlmRequest("status"));

        Assert.Equal("ok", result);

        var request = handler.LastRequest!;
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("router-key", request.Headers.Authorization?.Parameter);
        Assert.Equal("https://app.local", request.Headers.GetValues("HTTP-Referer").Single());
        Assert.Equal("Foundry", request.Headers.GetValues("X-Title").Single());
    }

    [Fact]
    public async Task OpenRouterClient_IncludesConversationHistory()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"choices":[{"message":{"content":"ok"}}]}
                """)
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openrouter.local/api/v1/")
        };

        var client = new OpenRouterLlmClient(httpClient, "router-key", "router-model", httpClient.BaseAddress);

        var history = new[]
        {
            new LlmMessage(MessageRole.User, "Hello"),
            new LlmMessage(MessageRole.Assistant, "Hi there"),
            new LlmMessage(MessageRole.User, "How are you?")
        };

        _ = await client.GetResponseAsync(new LlmRequest("What can you do?", history: history));

        using var document = JsonDocument.Parse(handler.LastRequestContent!);
        var messages = document.RootElement.GetProperty("messages");
        Assert.Equal(4, messages.GetArrayLength());
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.Equal("Hello", messages[0].GetProperty("content").GetString());
        Assert.Equal("assistant", messages[1].GetProperty("role").GetString());
        Assert.Equal("Hi there", messages[1].GetProperty("content").GetString());
        Assert.Equal("user", messages[2].GetProperty("role").GetString());
        Assert.Equal("How are you?", messages[2].GetProperty("content").GetString());
        Assert.Equal("user", messages[3].GetProperty("role").GetString());
        Assert.Equal("What can you do?", messages[3].GetProperty("content").GetString());
    }

    [Fact]
    public async Task OpenRouterClient_IncludesSystemInstructionsWhenRequested()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"choices":[{"message":{"content":"ok"}}]}
                """)
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openrouter.local/api/v1/")
        };

        var client = new OpenRouterLlmClient(httpClient, "router-key", "router-model", httpClient.BaseAddress);

        _ = await client.GetResponseAsync(new LlmRequest("Prompt", "Remember context.", includeInstructions: true));

        using var document = JsonDocument.Parse(handler.LastRequestContent!);
        var messages = document.RootElement.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("Remember context.", messages[0].GetProperty("content").GetString());
    }

    [Fact]
    public async Task OpenRouterClient_ThrowsOnErrorResponse()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("oops")
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://openrouter.local/api/v1/")
        };

        var client = new OpenRouterLlmClient(httpClient, "router-key", "router-model", httpClient.BaseAddress);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetResponseAsync(new LlmRequest("prompt")));
    }

    [Fact]
    public async Task OllamaClient_SendsExpectedPayload()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(new Uri("http://localhost:9000/api/chat"), request.RequestUri);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"message":{"content":"  hi  "}}
                    """)
            };
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:9000/")
        };

        var client = new OllamaLlmClient(httpClient, "llama3", httpClient.BaseAddress);
        var response = await client.GetResponseAsync(new LlmRequest("Hello there"));

        Assert.Equal("hi", response);

        using var document = JsonDocument.Parse(handler.LastRequestContent!);
        var root = document.RootElement;
        Assert.Equal("llama3", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        var ollamaMessages = root.GetProperty("messages");
        Assert.Equal(1, ollamaMessages.GetArrayLength());
        Assert.Equal("user", ollamaMessages[0].GetProperty("role").GetString());
        Assert.Equal("Hello there", ollamaMessages[0].GetProperty("content").GetString());
    }

    [Fact]
    public async Task OllamaClient_IncludesSystemInstructionsWhenRequested()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"message":{"content":"hi"}}
                """)
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:9000/")
        };

        var client = new OllamaLlmClient(httpClient, "llama3", httpClient.BaseAddress);

        _ = await client.GetResponseAsync(new LlmRequest("Prompt", "System instructions", includeInstructions: true));

        using var document = JsonDocument.Parse(handler.LastRequestContent!);
        var messages = document.RootElement.GetProperty("messages");
        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("System instructions", messages[0].GetProperty("content").GetString());
    }

    [Fact]
    public async Task OllamaClient_IncludesConversationHistory()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"message":{"content":"ok"}}
                """)
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:9000/")
        };

        var client = new OllamaLlmClient(httpClient, "llama3", httpClient.BaseAddress);

        var history = new[]
        {
            new LlmMessage(MessageRole.User, "Hello"),
            new LlmMessage(MessageRole.Assistant, "Hi there"),
            new LlmMessage(MessageRole.User, "How are you?")
        };

        _ = await client.GetResponseAsync(new LlmRequest("What can you do?", history: history));

        using var document = JsonDocument.Parse(handler.LastRequestContent!);
        var messages = document.RootElement.GetProperty("messages");
        Assert.Equal(4, messages.GetArrayLength());
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.Equal("Hello", messages[0].GetProperty("content").GetString());
        Assert.Equal("assistant", messages[1].GetProperty("role").GetString());
        Assert.Equal("Hi there", messages[1].GetProperty("content").GetString());
        Assert.Equal("user", messages[2].GetProperty("role").GetString());
        Assert.Equal("How are you?", messages[2].GetProperty("content").GetString());
        Assert.Equal("user", messages[3].GetProperty("role").GetString());
        Assert.Equal("What can you do?", messages[3].GetProperty("content").GetString());
    }

    [Fact]
    public async Task OllamaClient_ThrowsOnErrorResponse()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("problem")
        });

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:9000/")
        };

        var client = new OllamaLlmClient(httpClient, "llama3", httpClient.BaseAddress);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetResponseAsync(new LlmRequest("prompt")));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestContent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestContent = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return _responseFactory(request);
        }
    }
}
