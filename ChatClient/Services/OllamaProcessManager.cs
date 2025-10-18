using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ChatClient.Services;

public sealed class OllamaProcessManager : IOllamaProcessManager, IDisposable
{
    private const string DefaultEndpoint = "http://localhost:11434/";
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    private readonly IBackgroundProcessService _backgroundProcessService;
    private readonly HttpClient _httpClient;
    private bool _disposed;

    public OllamaProcessManager(IBackgroundProcessService backgroundProcessService, HttpClient? httpClient = null)
    {
        _backgroundProcessService = backgroundProcessService ?? throw new ArgumentNullException(nameof(backgroundProcessService));
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = ProbeTimeout
        };
    }

    public async Task<OllamaProcessEnsureResult> EnsureServerAsync(string? endpoint, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        endpoint ??= DefaultEndpoint;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
        {
            throw new ArgumentException($"Invalid Ollama endpoint '{endpoint}'.", nameof(endpoint));
        }

        if (await IsOllamaRespondingAsync(endpointUri, cancellationToken).ConfigureAwait(false))
        {
            return new OllamaProcessEnsureResult(true, null);
        }

        var request = CreateRequest(endpointUri);
        var snapshot = await _backgroundProcessService.EnsureRunningAsync(request, cancellationToken).ConfigureAwait(false);

        return new OllamaProcessEnsureResult(false, snapshot);
    }

    private BackgroundProcessRequest CreateRequest(Uri endpoint)
    {
        var port = endpoint.Port;
        var host = endpoint.Host;
        var hostPart = host.Contains(':', StringComparison.Ordinal)
            ? $"[{host}]"
            : host;
        var hostConfiguration = $"{hostPart}:{port}";

        return new BackgroundProcessRequest
        {
            Id = "ollama-daemon",
            DisplayName = "Ollama Server",
            Category = BackgroundProcessCategory.Server,
            Command = "ollama",
            Arguments = "serve",
            LogFileNamePrefix = "ollama",
            ReadinessTimeout = TimeSpan.FromSeconds(30),
            ReadinessProbeInterval = TimeSpan.FromMilliseconds(500),
            ReadinessProbe = token => IsOllamaRespondingAsync(endpoint, token),
            KillOnDispose = true,
            EnvironmentVariables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["OLLAMA_HOST"] = hostConfiguration
            }
        };
    }

    private async Task<bool> IsOllamaRespondingAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        try
        {
            var probeUri = new Uri(endpoint, "api/version");
            using var response = await _httpClient.GetAsync(probeUri, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(OllamaProcessManager));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _httpClient.Dispose();
    }
}
