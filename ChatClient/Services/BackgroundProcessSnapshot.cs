using System;

namespace ChatClient.Services;

public sealed record BackgroundProcessSnapshot(
    string Id,
    string DisplayName,
    BackgroundProcessCategory Category,
    string Command,
    string Arguments,
    string LogPath,
    DateTimeOffset StartedAt,
    int? ProcessId,
    bool IsRunning,
    bool IsHealthy,
    int? ExitCode,
    string? StatusMessage,
    bool ManagedByApplication);
