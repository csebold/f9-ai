using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ChatClient.Services;

/// <summary>
/// Downloads and caches provider favicons for use across the application.
/// </summary>
public sealed class ProviderBrandingService : IProviderBrandingService, IDisposable
{
    private static readonly IReadOnlyDictionary<LlmProvider, ProviderBrandingSource> BrandingSources =
        new Dictionary<LlmProvider, ProviderBrandingSource>
        {
            [LlmProvider.OpenAi] = new("OpenAI", new Uri("https://www.google.com/s2/favicons?sz=64&domain_url=openai.com", UriKind.Absolute)),
            [LlmProvider.Anthropic] = new("Anthropic", new Uri("https://www.google.com/s2/favicons?sz=64&domain_url=anthropic.com", UriKind.Absolute)),
            [LlmProvider.OpenRouter] = new("OpenRouter", new Uri("https://www.google.com/s2/favicons?sz=64&domain_url=openrouter.ai", UriKind.Absolute)),
            [LlmProvider.Ollama] = new("Ollama", new Uri("https://www.google.com/s2/favicons?sz=64&domain_url=ollama.com", UriKind.Absolute)),
        };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly Dictionary<LlmProvider, ProviderBranding> _cache = new();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private bool _disposed;

    public ProviderBrandingService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient is null;

        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Foundry9AI/1.0");
        }
    }

    public async Task<ProviderBranding> GetBrandingAsync(LlmProvider provider, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_cache.TryGetValue(provider, out var cached) && IconExists(cached))
        {
            return cached;
        }

        var existing = await LoadFromCacheAsync(provider).ConfigureAwait(false);
        if (IconExists(existing))
        {
            _cache[provider] = existing;
            return existing;
        }

        try
        {
            var refreshed = await RefreshAsync(provider, cancellationToken).ConfigureAwait(false);
            return refreshed;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            _cache[provider] = existing;
            return existing;
        }
    }

    public async Task<IReadOnlyDictionary<LlmProvider, ProviderBranding>> RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var results = new Dictionary<LlmProvider, ProviderBranding>();

        foreach (var provider in BrandingSources.Keys)
        {
            try
            {
                var branding = await RefreshAsync(provider, cancellationToken).ConfigureAwait(false);
                results[provider] = branding;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                var fallback = await LoadFromCacheAsync(provider).ConfigureAwait(false);
                results[provider] = fallback;
            }
        }

        return results;
    }

    public async Task<ProviderBranding> RefreshAsync(LlmProvider provider, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!BrandingSources.TryGetValue(provider, out var source))
        {
            throw new InvalidOperationException($"Unsupported provider '{provider}'.");
        }

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var response = await _httpClient.GetAsync(source.IconUri, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var extension = GetFileExtension(response);
            var targetPath = AppPaths.GetProviderFaviconPath(provider, extension);
            var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

            if (File.Exists(targetPath))
            {
                try
                {
                    var existingBytes = await File.ReadAllBytesAsync(targetPath, cancellationToken).ConfigureAwait(false);
                    if (payload.AsSpan().SequenceEqual(existingBytes.AsSpan()))
                    {
                        var cached = new ProviderBranding(provider, source.DisplayName, targetPath);
                        _cache[provider] = cached;
                        return cached;
                    }
                }
                catch (IOException)
                {
                    // If we can't read the existing file, fall through to rewrite it.
                }
            }

            DeleteStaleBrandingFiles(provider, targetPath);
            await File.WriteAllBytesAsync(targetPath, payload, cancellationToken).ConfigureAwait(false);

            var updated = new ProviderBranding(provider, source.DisplayName, targetPath);
            _cache[provider] = updated;
            return updated;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            var fallback = await LoadFromCacheAsync(provider).ConfigureAwait(false);
            _cache[provider] = fallback;
            return fallback;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _refreshLock.Dispose();

        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static ProviderBrandingSource GetBrandingSource(LlmProvider provider)
    {
        if (!BrandingSources.TryGetValue(provider, out var source))
        {
            throw new InvalidOperationException($"Unsupported provider '{provider}'.");
        }

        return source;
    }

    private static bool IconExists(ProviderBranding branding) =>
        branding.IconPath is not null && File.Exists(branding.IconPath);

    private static string GetFileExtension(HttpResponseMessage response)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
        return mediaType switch
        {
            "image/png" => ".png",
            "image/svg+xml" => ".svg",
            "image/x-icon" => ".ico",
            "image/vnd.microsoft.icon" => ".ico",
            _ => ".ico"
        };
    }

    private static void DeleteStaleBrandingFiles(LlmProvider provider, string keepPath)
    {
        var directory = AppPaths.GetProviderBrandingDirectory();
        var prefix = $"{provider.ToString().ToLowerInvariant()}.";
        var keepFullPath = Path.GetFullPath(keepPath);

        try
        {
            foreach (var path in Directory.GetFiles(directory, $"{prefix}*"))
            {
                if (!string.Equals(Path.GetFullPath(path), keepFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch
                    {
                        // Ignore failures while cleaning up stale files.
                    }
                }
            }
        }
        catch
        {
            // Ignore cleanup errors; caching will continue to work.
        }
    }

    private Task<ProviderBranding> LoadFromCacheAsync(LlmProvider provider)
    {
        var source = GetBrandingSource(provider);

        var directory = AppPaths.GetProviderBrandingDirectory();
        var prefix = $"{provider.ToString().ToLowerInvariant()}.";
        string? existingPath = null;

        try
        {
            existingPath = Directory.GetFiles(directory, $"{prefix}*")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch
        {
            existingPath = null;
        }

        var branding = new ProviderBranding(provider, source.DisplayName, existingPath);
        _cache[provider] = branding;
        return Task.FromResult(branding);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ProviderBrandingService));
        }
    }

    private sealed record ProviderBrandingSource(string DisplayName, Uri IconUri);
}
