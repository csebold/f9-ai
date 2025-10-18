using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChatClient.Services;

public sealed class BackgroundProcessService : IBackgroundProcessService
{
    private readonly ConcurrentDictionary<string, BackgroundProcessRegistration> _registrations = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private bool _disposed;

    public event EventHandler<BackgroundProcessChangedEventArgs>? ProcessChanged;

    public IReadOnlyCollection<BackgroundProcessSnapshot> GetProcesses()
        => _registrations.Values.Select(reg => reg.CreateSnapshot()).ToArray();

    public async Task<BackgroundProcessSnapshot> EnsureRunningAsync(BackgroundProcessRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        ValidateRequest(request);
        ThrowIfDisposed();

        BackgroundProcessRegistration registration;
        var logPath = CreateLogPath(request);

        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_registrations.TryGetValue(request.Id, out var existing))
            {
                var snapshot = existing.CreateSnapshot();
                if (snapshot.IsRunning)
                {
                    return snapshot;
                }

                await RemoveRegistrationAsync(request.Id, existing, BackgroundProcessChangeKind.Updated).ConfigureAwait(false);
            }

            registration = StartProcess(request, logPath);
            _registrations[request.Id] = registration;
        }
        finally
        {
            _stateLock.Release();
        }

        RaiseProcessChanged(BackgroundProcessChangeKind.Added, registration.CreateSnapshot());

        if (request.ReadinessProbe is null)
        {
            registration.SetHealthy("Process running.");
            var snapshot = registration.CreateSnapshot();
            RaiseProcessChanged(BackgroundProcessChangeKind.Updated, snapshot);
            return snapshot;
        }

        var healthy = false;
        Exception? readinessException = null;
        var deadline = DateTime.UtcNow + request.ReadinessTimeout;

        while (!healthy && DateTime.UtcNow <= deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!registration.IsRunning)
            {
                readinessException = new InvalidOperationException("Process exited before reporting ready.");
                break;
            }

            try
            {
                healthy = await request.ReadinessProbe(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                readinessException = ex;
            }

            if (healthy)
            {
                break;
            }

            await Task.Delay(request.ReadinessProbeInterval, cancellationToken).ConfigureAwait(false);
        }

        if (healthy)
        {
            registration.SetHealthy("Process is ready.");
        }
        else
        {
            var reason = readinessException is null
                ? "Process did not report as ready before timeout."
                : $"Process readiness probe failed: {readinessException.Message}";
            registration.SetUnhealthy(reason);
        }

        var updatedSnapshot = registration.CreateSnapshot();
        RaiseProcessChanged(BackgroundProcessChangeKind.Updated, updatedSnapshot);
        return updatedSnapshot;
    }

    public async Task<bool> StopAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Process id must be provided.", nameof(id));
        }

        ThrowIfDisposed();

        BackgroundProcessRegistration? registration = null;

        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_registrations.TryGetValue(id, out registration))
            {
                _registrations.TryRemove(id, out _);
            }
        }
        finally
        {
            _stateLock.Release();
        }

        if (registration is null)
        {
            return false;
        }

        await TerminateAsync(registration).ConfigureAwait(false);
        RaiseProcessChanged(BackgroundProcessChangeKind.Removed, registration.CreateSnapshot());
        return true;
    }

    public Task<BackgroundProcessSnapshot?> GetSnapshotAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Process id must be provided.", nameof(id));
        }

        ThrowIfDisposed();

        _registrations.TryGetValue(id, out var registration);
        return Task.FromResult(registration?.CreateSnapshot());
    }

    public async Task OpenTerminalAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Process id must be provided.", nameof(id));
        }

        ThrowIfDisposed();

        if (!_registrations.TryGetValue(id, out var registration))
        {
            throw new InvalidOperationException($"No process with id '{id}' is registered.");
        }

        var logPath = registration.LogPath;
        if (!File.Exists(logPath))
        {
            throw new FileNotFoundException("Log file for process could not be found.", logPath);
        }

        await LaunchNativeTerminalAsync(registration, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var registration in _registrations.Values)
        {
            try
            {
                if (registration.Request.KillOnDispose && registration.IsRunning)
                {
                    try
                    {
                        registration.Process.Kill(entireProcessTree: true);
                        registration.Process.WaitForExit(2000);
                    }
                    catch
                    {
                        // Ignore shutdown failures.
                    }
                }

                registration.Dispose();
            }
            catch
            {
                // Ignore cleanup failures during disposal.
            }
        }

        _registrations.Clear();
        _stateLock.Dispose();
    }

    private static void ValidateRequest(BackgroundProcessRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Id))
        {
            throw new ArgumentException("Process id must be provided.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ArgumentException("Display name must be provided.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Command))
        {
            throw new ArgumentException("Command must be provided.", nameof(request));
        }

        if (request.ReadinessTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("Readiness timeout must be positive.", nameof(request));
        }

        if (request.ReadinessProbeInterval <= TimeSpan.Zero)
        {
            throw new ArgumentException("Readiness probe interval must be positive.", nameof(request));
        }
    }

    private static string CreateLogPath(BackgroundProcessRequest request)
    {
        var prefix = string.IsNullOrWhiteSpace(request.LogFileNamePrefix)
            ? request.Id
            : request.LogFileNamePrefix.Trim();
        var safePrefix = SanitizeFileName(prefix);
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMddHHmmss");
        var fileName = $"{safePrefix}-{timestamp}.log";

        var logsDirectory = AppPaths.GetLogsDirectory();
        return Path.Combine(logsDirectory, fileName);
    }

    private BackgroundProcessRegistration StartProcess(BackgroundProcessRequest request, string logPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.Command,
            Arguments = request.Arguments,
            WorkingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory)
                ? AppContext.BaseDirectory
                : request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = false,
            CreateNoWindow = true
        };

        foreach (var pair in request.EnvironmentVariables)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start process '{request.Command}'.");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to start process '{request.Command}': {ex.Message}", ex);
        }

        var logWriter = CreateLogWriter(logPath);
        var registration = new BackgroundProcessRegistration(request, process, logPath, logWriter);

        process.OutputDataReceived += (_, args) => registration.AppendLogLine(args.Data, isError: false);
        process.ErrorDataReceived += (_, args) => registration.AppendLogLine(args.Data, isError: true);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.Exited += (_, _) => OnProcessExited(registration);

        return registration;
    }

    private void OnProcessExited(BackgroundProcessRegistration registration)
    {
        try
        {
            registration.MarkExited();
            var snapshot = registration.CreateSnapshot();
            RaiseProcessChanged(BackgroundProcessChangeKind.Updated, snapshot);
        }
        catch
        {
            // Ignore exceptions coming from exit handling.
        }
    }

    private static StreamWriter CreateLogWriter(string logPath)
    {
        var stream = new FileStream(
            logPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);
        return new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true
        };
    }

    private async Task RemoveRegistrationAsync(string id, BackgroundProcessRegistration registration, BackgroundProcessChangeKind reason)
    {
        _registrations.TryRemove(id, out _);
        var snapshot = await TerminateAsync(registration).ConfigureAwait(false);
        RaiseProcessChanged(reason, snapshot);
    }

    private static async Task<BackgroundProcessSnapshot> TerminateAsync(BackgroundProcessRegistration registration)
    {
        BackgroundProcessSnapshot snapshot;
        try
        {
            if (!registration.Process.HasExited)
            {
                registration.Process.Kill(entireProcessTree: true);
                await registration.Process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        catch
        {
            // Ignore termination failures; process might already be gone.
        }
        finally
        {
            registration.MarkExited();
            snapshot = registration.CreateSnapshot();
            registration.Dispose();
        }

        return snapshot;
    }

    private async Task LaunchNativeTerminalAsync(BackgroundProcessRegistration registration, CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo;
        if (OperatingSystem.IsWindows())
        {
            var escapedTitle = EscapeForCmd(registration.Request.DisplayName);
            var windowsTailCommand = BuildWindowsTailCommand(registration.LogPath);
            var escapedCommand = EscapeForCmd(windowsTailCommand);
            startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c start \"{escapedTitle}\" powershell -NoLogo -NoProfile -NoExit -Command {escapedCommand}",
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else if (OperatingSystem.IsMacOS())
        {
            var logPath = registration.LogPath;
            startInfo = new ProcessStartInfo
            {
                FileName = "open",
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-a");
            startInfo.ArgumentList.Add("Console");
            startInfo.ArgumentList.Add(logPath);
        }
        else
        {
            // Assume Linux/Unix. Try x-terminal-emulator first, fall back to gnome-terminal if available.
            var terminalCommand = "x-terminal-emulator";
            if (!IsCommandAvailable(terminalCommand))
            {
                terminalCommand = "gnome-terminal";
            }

            var tailCommand = BuildPosixTailCommand(registration.LogPath);
            var minimalShellCommand = $"env -i PATH=/usr/bin:/bin:/usr/sbin:/sbin /bin/sh -c {ShellQuote(tailCommand)}";
            var linuxCommand = $"bash --noprofile --norc -lc {ShellQuote(minimalShellCommand)}";
            startInfo = new ProcessStartInfo
            {
                FileName = terminalCommand,
                Arguments = $"-- {linuxCommand}",
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = true
            };
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to open terminal window.");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to open terminal window: {ex.Message}", ex);
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static string EscapeForCmd(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static string BuildWindowsTailCommand(string logPath) =>
        $"Get-Content -Path '{logPath.Replace("'", "''")}' -Tail 50 -Wait";

    private static string BuildPosixTailCommand(string logPath) =>
        $"exec /usr/bin/env tail -n 50 -F -- {ShellQuote(logPath)}";

    private static string ShellQuote(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "''";
        }

        return "'" + value.Replace("'", "'\"'\"'") + "'";
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);

        foreach (var ch in value)
        {
            builder.Append(invalidChars.Contains(ch) ? '_' : ch);
        }

        return builder.ToString();
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(1500);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private void RaiseProcessChanged(BackgroundProcessChangeKind kind, BackgroundProcessSnapshot snapshot)
    {
        try
        {
            ProcessChanged?.Invoke(this, new BackgroundProcessChangedEventArgs(kind, snapshot));
        }
        catch
        {
            // Ignore failures from event subscribers.
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(BackgroundProcessService));
        }
    }

    private sealed class BackgroundProcessRegistration : IDisposable
    {
        private readonly object _lock = new();
        private readonly StreamWriter _logWriter;
        private bool _disposed;

        public BackgroundProcessRegistration(BackgroundProcessRequest request, Process process, string logPath, StreamWriter logWriter)
        {
            Request = request;
            Process = process;
            LogPath = logPath;
            _logWriter = logWriter;
            StartedAt = DateTimeOffset.Now;
            StatusMessage = "Starting...";
            ManagedByApplication = request.KillOnDispose;
        }

        public BackgroundProcessRequest Request { get; }

        public Process Process { get; }

        public string LogPath { get; }

        public DateTimeOffset StartedAt { get; }

        public bool ManagedByApplication { get; }

        public bool IsHealthy { get; private set; }

        public string? StatusMessage { get; private set; }

        public int? ExitCode => Process.HasExited ? Process.ExitCode : null;

        public bool IsRunning => !Process.HasExited;

        public void AppendLogLine(string? data, bool isError)
        {
            if (string.IsNullOrEmpty(data))
            {
                return;
            }

            lock (_lock)
            {
                _logWriter.WriteLine($"{DateTimeOffset.Now:O} [{(isError ? "ERR" : "OUT")}] {data}");
            }
        }

        public void SetHealthy(string message)
        {
            IsHealthy = true;
            StatusMessage = message;
        }

        public void SetUnhealthy(string message)
        {
            IsHealthy = false;
            StatusMessage = message;
        }

        public void MarkExited()
        {
            StatusMessage = $"Exited with code {ExitCode?.ToString() ?? "unknown"}.";
        }

        public BackgroundProcessSnapshot CreateSnapshot()
        {
            return new BackgroundProcessSnapshot(
                Request.Id,
                Request.DisplayName,
                Request.Category,
                Request.Command,
                Request.Arguments,
                LogPath,
                StartedAt,
                IsRunning,
                IsHealthy,
                ExitCode,
                StatusMessage,
                ManagedByApplication);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Process.Dispose();
            _logWriter.Dispose();
        }
    }
}
