using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Services;
using Xunit;

namespace ChatClient.Tests.Services;

public class ProviderBrandingServiceTests
{
    [Fact]
    public async Task RefreshAsync_WritesIconWithContentTypeExtension()
    {
        var tempRoot = CreateTemporaryDirectory();
        try
        {
            using var scope = AppPaths.OverrideBaseDirectoryForTesting(tempRoot);
            var handler = new StubHttpMessageHandler(_ => CreateImageResponse("image/png", new byte[] { 1, 2, 3, 4 }));
            using var httpClient = new HttpClient(handler);
            using var service = new ProviderBrandingService(httpClient);

            var branding = await service.RefreshAsync(LlmProvider.OpenAi, CancellationToken.None);

            var expectedPath = Path.Combine(AppPaths.GetProviderBrandingDirectory(), "openai.png");
            Assert.Equal(expectedPath, branding.IconPath);
            Assert.True(File.Exists(expectedPath));
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(expectedPath));
            Assert.Equal(1, handler.RequestCount);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RefreshAsync_RemovesStaleFilesWithDifferentExtension()
    {
        var tempRoot = CreateTemporaryDirectory();
        try
        {
            using var scope = AppPaths.OverrideBaseDirectoryForTesting(tempRoot);
            var brandingDirectory = AppPaths.GetProviderBrandingDirectory();
            var stalePath = Path.Combine(brandingDirectory, "anthropic.ico");
            await File.WriteAllBytesAsync(stalePath, new byte[] { 9, 9, 9 });

            var handler = new StubHttpMessageHandler(_ => CreateImageResponse("image/png", new byte[] { 4, 5, 6 }));
            using var httpClient = new HttpClient(handler);
            using var service = new ProviderBrandingService(httpClient);

            var branding = await service.RefreshAsync(LlmProvider.Anthropic, CancellationToken.None);

            var expectedPath = Path.Combine(brandingDirectory, "anthropic.png");
            Assert.Equal(expectedPath, branding.IconPath);
            Assert.True(File.Exists(expectedPath));
            Assert.False(File.Exists(stalePath));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RefreshAsync_ReturnsCachedBrandingWhenRequestFails()
    {
        var tempRoot = CreateTemporaryDirectory();
        try
        {
            using var scope = AppPaths.OverrideBaseDirectoryForTesting(tempRoot);
            var brandingDirectory = AppPaths.GetProviderBrandingDirectory();
            var cachedPath = Path.Combine(brandingDirectory, "openrouter.png");
            var cachedBytes = new byte[] { 7, 7, 7 };
            await File.WriteAllBytesAsync(cachedPath, cachedBytes);

            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            using var httpClient = new HttpClient(handler);
            using var service = new ProviderBrandingService(httpClient);

            var branding = await service.RefreshAsync(LlmProvider.OpenRouter, CancellationToken.None);

            Assert.Equal(cachedPath, branding.IconPath);
            Assert.True(File.Exists(cachedPath));
            Assert.Equal(cachedBytes, await File.ReadAllBytesAsync(cachedPath));
            Assert.Equal(1, handler.RequestCount);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GetBrandingAsync_UsesCacheWithoutNetwork()
    {
        var tempRoot = CreateTemporaryDirectory();
        try
        {
            using var scope = AppPaths.OverrideBaseDirectoryForTesting(tempRoot);
            var brandingDirectory = AppPaths.GetProviderBrandingDirectory();
            var cachedPath = Path.Combine(brandingDirectory, "ollama.png");
            await File.WriteAllBytesAsync(cachedPath, new byte[] { 3, 2, 1 });

            var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("Network should not be invoked."));
            using var httpClient = new HttpClient(handler);
            using var service = new ProviderBrandingService(httpClient);

            var branding = await service.GetBrandingAsync(LlmProvider.Ollama, CancellationToken.None);

            Assert.Equal(cachedPath, branding.IconPath);
            Assert.Equal(0, handler.RequestCount);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static HttpResponseMessage CreateImageResponse(string mediaType, byte[] payload)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(payload)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        return response;
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"f9-branding-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(_responseFactory(request));
        }
    }
}
