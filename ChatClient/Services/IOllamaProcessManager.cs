using System;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Models;

namespace ChatClient.Services;

public interface IOllamaProcessManager : IDisposable
{
    Task<OllamaProcessEnsureResult> EnsureServerAsync(string? endpoint, CancellationToken cancellationToken);

    Task<bool> StopServerAsync(CancellationToken cancellationToken);

    void UpdateEnvironmentDefaults(OllamaRuntimeSettings settings);
}
