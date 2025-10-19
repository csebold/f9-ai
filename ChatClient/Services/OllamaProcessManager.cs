using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Models;
namespace ChatClient.Services;

public sealed class OllamaProcessManager : IOllamaProcessManager, IDisposable
{
    private const string DefaultEndpoint = "http://localhost:11434/";
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(3);

    private readonly IBackgroundProcessService _backgroundProcessService;
    private readonly HttpClient _httpClient;
    private bool _disposed;
    private readonly HashSet<int> _trackedProcessIds = new();
    private OllamaRuntimeSettings _runtimeDefaults = new();

    public OllamaProcessManager(IBackgroundProcessService backgroundProcessService, HttpClient? httpClient = null)
    {
        _backgroundProcessService = backgroundProcessService ?? throw new ArgumentNullException(nameof(backgroundProcessService));
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = ProbeTimeout
        };

        _backgroundProcessService.ProcessChanged += OnBackgroundProcessChanged;
    }

    public async Task<OllamaProcessEnsureResult> EnsureServerAsync(string? endpoint, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        endpoint ??= DefaultEndpoint;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
        {
            throw new ArgumentException($"Invalid Ollama endpoint '{endpoint}'.", nameof(endpoint));
        }

        var existingSnapshot = await _backgroundProcessService.GetSnapshotAsync("ollama-daemon", cancellationToken).ConfigureAwait(false);
        if (existingSnapshot is not null && existingSnapshot.IsRunning)
        {
            TrackManagedProcess(existingSnapshot);
            return new OllamaProcessEnsureResult(false, existingSnapshot);
        }

        if (existingSnapshot is not null && existingSnapshot.ProcessId is int stalePid)
        {
            UntrackProcess(stalePid);
        }

        if (await IsOllamaRespondingAsync(endpointUri, cancellationToken).ConfigureAwait(false))
        {
            var externalSnapshot = await TryCreateExternalSnapshotAsync(endpointUri, cancellationToken).ConfigureAwait(false);
            return new OllamaProcessEnsureResult(true, externalSnapshot);
        }

        var request = CreateRequest(endpointUri);
        var snapshot = await _backgroundProcessService.EnsureRunningAsync(request, cancellationToken).ConfigureAwait(false);

        TrackManagedProcess(snapshot);
        return new OllamaProcessEnsureResult(false, snapshot);
    }

    public void UpdateEnvironmentDefaults(OllamaRuntimeSettings settings)
    {
        var copy = settings is null
            ? new OllamaRuntimeSettings()
            : new OllamaRuntimeSettings
            {
                MaxLoadedModels = settings.MaxLoadedModels,
                NumParallelRequests = settings.NumParallelRequests,
                MaxQueue = settings.MaxQueue
            };

        Interlocked.Exchange(ref _runtimeDefaults, copy);
    }

    private BackgroundProcessRequest CreateRequest(Uri endpoint)
    {
        var port = endpoint.Port;
        var host = endpoint.Host;
        var hostPart = host.Contains(':', StringComparison.Ordinal)
            ? $"[{host}]"
            : host;
        var hostConfiguration = $"{hostPart}:{port}";

        var request = new BackgroundProcessRequest
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

        ApplyRuntimeEnvironment(request.EnvironmentVariables);
        return request;
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

    public async Task<bool> StopServerAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var snapshot = await _backgroundProcessService.GetSnapshotAsync("ollama-daemon", cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return false;
        }

        await _backgroundProcessService.StopAsync("ollama-daemon", cancellationToken).ConfigureAwait(false);
        if (snapshot.ProcessId is int pid)
        {
            UntrackProcess(pid);
        }
        return true;
    }

    private void TrackManagedProcess(BackgroundProcessSnapshot snapshot)
    {
        if (!snapshot.ManagedByApplication)
        {
            return;
        }

        if (snapshot.ProcessId is not int pid || pid <= 0)
        {
            return;
        }

        _trackedProcessIds.Add(pid);
        OllamaProcessRegistry.AddOrUpdate(pid, snapshot.LogPath);
    }

    private void UntrackProcess(int pid)
    {
        if (pid <= 0)
        {
            return;
        }

        if (_trackedProcessIds.Remove(pid))
        {
            OllamaProcessRegistry.Remove(pid);
        }
        else
        {
            OllamaProcessRegistry.Remove(pid);
        }
    }

    private void OnBackgroundProcessChanged(object? sender, BackgroundProcessChangedEventArgs e)
    {
        if (!string.Equals(e.Snapshot.Id, "ollama-daemon", StringComparison.Ordinal))
        {
            return;
        }

        if (!e.Snapshot.IsRunning || e.Kind == BackgroundProcessChangeKind.Removed)
        {
            if (e.Snapshot.ProcessId is int pid)
            {
                UntrackProcess(pid);
            }
        }
        else if (e.Snapshot.ManagedByApplication)
        {
            TrackManagedProcess(e.Snapshot);
        }
    }

    private async Task<BackgroundProcessSnapshot?> TryCreateExternalSnapshotAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ollama",
                ArgumentList = { "ps", "--json" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var hostPart = endpoint.Host.Contains(':', StringComparison.Ordinal)
                ? $"[{endpoint.Host}]"
                : endpoint.Host;
            startInfo.Environment["OLLAMA_HOST"] = $"{hostPart}:{endpoint.Port}";
            ApplyRuntimeEnvironment((key, value) => startInfo.Environment[key] = value);

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return CreateFallbackSnapshot();
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await using var _ = cts.Token.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                }
            });

#if NET9_0_OR_GREATER
            var output = await process.StandardOutput.ReadToEndAsync(cts.Token).ConfigureAwait(false);
#else
            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
#endif
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                return CreateFallbackSnapshot();
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                return CreateFallbackSnapshot();
            }

            using var document = JsonDocument.Parse(output);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("models", out var modelsElement) || modelsElement.ValueKind != JsonValueKind.Array)
            {
                return CreateFallbackSnapshot();
            }

            DateTimeOffset startedAt = DateTimeOffset.Now;
            string statusMessage = "Detected external Ollama server.";
            int? detectedPid = null;

            foreach (var model in modelsElement.EnumerateArray())
            {
                if (model.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (model.TryGetProperty("pid", out var pidElement))
                {
                    if (pidElement.ValueKind == JsonValueKind.Number && pidElement.TryGetInt32(out var pidValue))
                    {
                        detectedPid = pidValue;
                    }
                }

                if (model.TryGetProperty("started_at", out var startedAtElement) && startedAtElement.ValueKind == JsonValueKind.String)
                {
                    if (DateTimeOffset.TryParse(startedAtElement.GetString(), out var parsed))
                    {
                        startedAt = parsed;
                    }
                }

                if (model.TryGetProperty("status", out var statusElement) && statusElement.ValueKind == JsonValueKind.String)
                {
                    var statusText = statusElement.GetString();
                    if (!string.IsNullOrWhiteSpace(statusText))
                    {
                        statusMessage = $"External server: {statusText}";
                    }
                }

                break;
            }

            return new BackgroundProcessSnapshot(
                Id: "ollama-external",
                DisplayName: "Ollama Server (external)",
                Category: BackgroundProcessCategory.Server,
                Command: "ollama",
                Arguments: "serve",
                LogPath: string.Empty,
                StartedAt: startedAt,
                ProcessId: detectedPid,
                IsRunning: true,
                IsHealthy: true,
                ExitCode: null,
                StatusMessage: statusMessage,
                ManagedByApplication: false);
        }
        catch
        {
            return CreateFallbackSnapshot();
        }

        static BackgroundProcessSnapshot CreateFallbackSnapshot()
        {
            return new BackgroundProcessSnapshot(
                Id: "ollama-external",
                DisplayName: "Ollama Server (external)",
                Category: BackgroundProcessCategory.Server,
                Command: "ollama",
                Arguments: "serve",
                LogPath: string.Empty,
                StartedAt: DateTimeOffset.Now,
                ProcessId: null,
                IsRunning: true,
                IsHealthy: true,
                ExitCode: null,
                StatusMessage: "Detected external Ollama server.",
                ManagedByApplication: false);
        }
    }

    private void ApplyRuntimeEnvironment(IDictionary<string, string> environment) =>
        ApplyRuntimeEnvironment((key, value) => environment[key] = value);

    private void ApplyRuntimeEnvironment(Action<string, string> assign)
    {
        var runtime = Volatile.Read(ref _runtimeDefaults);
        assign("OLLAMA_MAX_LOADED_MODELS", runtime.MaxLoadedModels.ToString(CultureInfo.InvariantCulture));
        assign("OLLAMA_NUM_PARALLEL", runtime.NumParallelRequests.ToString(CultureInfo.InvariantCulture));
        assign("OLLAMA_MAX_QUEUE", runtime.MaxQueue.ToString(CultureInfo.InvariantCulture));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _httpClient.Dispose();
        _backgroundProcessService.ProcessChanged -= OnBackgroundProcessChanged;
        foreach (var pid in _trackedProcessIds)
        {
            OllamaProcessRegistry.Remove(pid);
        }
        _trackedProcessIds.Clear();
    }
}
