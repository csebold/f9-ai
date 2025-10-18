using System;
using System.Threading;
using System.Threading.Tasks;

namespace ChatClient.Services;

public interface IOllamaProcessManager : IDisposable
{
    Task<OllamaProcessEnsureResult> EnsureServerAsync(string? endpoint, CancellationToken cancellationToken);

    Task<bool> StopServerAsync(CancellationToken cancellationToken);
}
