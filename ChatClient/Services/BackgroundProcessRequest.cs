using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ChatClient.Services;

public sealed class BackgroundProcessRequest
{
    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public BackgroundProcessCategory Category { get; init; } = BackgroundProcessCategory.Server;

    public string Command { get; init; } = string.Empty;

    public string Arguments { get; init; } = string.Empty;

    public string? WorkingDirectory { get; init; }

    public Dictionary<string, string> EnvironmentVariables { get; init; } = new(StringComparer.Ordinal);

    public TimeSpan ReadinessTimeout { get; init; } = TimeSpan.FromSeconds(20);

    public TimeSpan ReadinessProbeInterval { get; init; } = TimeSpan.FromMilliseconds(250);

    public Func<CancellationToken, Task<bool>>? ReadinessProbe { get; init; }

    public bool KillOnDispose { get; init; } = true;

    public string? LogFileNamePrefix { get; init; }
}
