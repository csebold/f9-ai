using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ChatClient.Services;

public interface IBackgroundProcessService : IDisposable
{
    event EventHandler<BackgroundProcessChangedEventArgs>? ProcessChanged;

    IReadOnlyCollection<BackgroundProcessSnapshot> GetProcesses();

    Task<BackgroundProcessSnapshot> EnsureRunningAsync(BackgroundProcessRequest request, CancellationToken cancellationToken);

    Task<bool> StopAsync(string id, CancellationToken cancellationToken);

    Task<BackgroundProcessSnapshot?> GetSnapshotAsync(string id, CancellationToken cancellationToken);

    Task OpenTerminalAsync(string id, CancellationToken cancellationToken);
}
