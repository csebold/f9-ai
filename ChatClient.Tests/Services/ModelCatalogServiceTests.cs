using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Models;
using ChatClient.Services;

namespace ChatClient.Tests.Services;

public class ModelCatalogServiceTests
{
    [Fact]
    public async Task GetModelsAsync_OpenAi_ReturnsSortedDistinctList()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"data":[{"id":"gpt-4"},{"id":"gpt-3.5"},{"id":"gpt-4"}]}
                    """)
            };
        });

        using var httpClient = new HttpClient(handler);
        using var service = new ModelCatalogService(httpClient);

        var models = await service.GetModelsAsync(
            LlmProvider.OpenAi,
            new ProviderSettings { ApiKey = "key" },
            CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal(new Uri("https://api.openai.com/v1/models"), capturedRequest!.RequestUri);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
        Assert.Equal("key", capturedRequest.Headers.Authorization?.Parameter);
        Assert.Equal(new[] { "gpt-3.5", "gpt-4" }, models.Select(m => m.Id));
        Assert.All(models, m =>
        {
            Assert.True(m.IsInstalled);
            Assert.False(m.IsDownloadable);
        });
    }

    [Fact]
    public async Task GetModelsAsync_Ollama_UsesConfiguredEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri is not null &&
                request.RequestUri.AbsolutePath.EndsWith("/api/tags", StringComparison.Ordinal))
            {
                capturedRequest ??= request;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        {"models":[{"name":"llama3"},{"name":"wizard"}]}
                        """)
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html></html>")
            };
        });

        using var httpClient = new HttpClient(handler);
        using var service = new ModelCatalogService(httpClient);

        var models = await service.GetModelsAsync(
            LlmProvider.Ollama,
            new ProviderSettings { Endpoint = "http://localhost:12345/" },
            CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal(new Uri("http://localhost:12345/api/tags"), capturedRequest!.RequestUri);
        Assert.Equal(HttpMethod.Get, capturedRequest.Method);
        Assert.Equal(new[] { "llama3", "wizard" }, models.Select(m => m.Id));
    }

    [Fact]
    public async Task GetModelsAsync_Ollama_ReturnsInstalledWhenLibraryFails()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri is not null &&
                request.RequestUri.AbsolutePath.EndsWith("/api/tags", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        {"models":[{"name":"mixtral"},{"name":"local/custom"}]}
                        """)
                };
            }

            if (request.RequestUri is not null &&
                request.RequestUri == new Uri("https://ollama.com/library"))
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("boom")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var service = new ModelCatalogService(new HttpClient(handler));

        var models = await service.GetModelsAsync(
            LlmProvider.Ollama,
            new ProviderSettings { Endpoint = "http://localhost:11434/" },
            CancellationToken.None);

        Assert.Equal(new[] { "local/custom", "mixtral" }, models.Select(m => m.Id));
        Assert.All(models, m => Assert.True(m.IsInstalled));
        Assert.All(models, m => Assert.False(m.IsDownloadable));
    }

    [Fact]
    public async Task GetModelsAsync_Ollama_ThrowsForInvalidEndpoint()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        using var service = new ModelCatalogService(httpClient);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetModelsAsync(
            LlmProvider.Ollama,
            new ProviderSettings { Endpoint = "not-a-url" },
            CancellationToken.None));

        Assert.Contains("Invalid Ollama endpoint", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetModelsAsync_ThrowsWhenApiKeyMissing()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        using var service = new ModelCatalogService(httpClient);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetModelsAsync(
            LlmProvider.OpenAi,
            new ProviderSettings { ApiKey = "" },
            CancellationToken.None));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _factory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> factory)
        {
            _factory = factory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_factory(request));
        }
    }
}
