using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ChatClient.Services;

/// <summary>
/// Manages chat log files and provides handles for appending structured entries.
/// </summary>
public sealed class ChatLogService : IDisposable
{
    private readonly Dictionary<string, ChatLogHandle> _handles = new(StringComparer.Ordinal);
    private bool _disposed;

    public ChatLogHandle GetOrCreate(string sessionId, string? existingPath = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Session id must be provided.", nameof(sessionId));
        }

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ChatLogService));
        }

        if (_handles.TryGetValue(sessionId, out var handle))
        {
            if (!string.IsNullOrWhiteSpace(existingPath) &&
                !string.Equals(existingPath, handle.Path, StringComparison.Ordinal))
            {
                handle = CreateHandle(sessionId, existingPath);
                _handles[sessionId] = handle;
            }

            return handle;
        }

        handle = CreateHandle(sessionId, existingPath);
        _handles[sessionId] = handle;
        return handle;
    }

    private static ChatLogHandle CreateHandle(string sessionId, string? existingPath)
    {
        var path = string.IsNullOrWhiteSpace(existingPath)
            ? GeneratePath(sessionId)
            : NormalizePath(existingPath);

        return new ChatLogHandle(sessionId, path);
    }

    private static string GeneratePath(string sessionId)
    {
        var logsDirectory = AppPaths.GetLogsDirectory();
        var sanitizedId = new string(sessionId.Where(char.IsLetterOrDigit).Take(12).ToArray());
        if (string.IsNullOrWhiteSpace(sanitizedId))
        {
            sanitizedId = "session";
        }

        var fileName = $"chat-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{sanitizedId.ToLowerInvariant()}.log";
        return Path.Combine(logsDirectory, fileName);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Log path must be provided.", nameof(path));
        }

        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(AppPaths.GetLogsDirectory(), path);
        }

        path = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return path;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _handles.Clear();
        _disposed = true;
    }
}

/// <summary>
/// Represents an append-only chat log for a specific session.
/// </summary>
public sealed class ChatLogHandle
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    internal ChatLogHandle(string sessionId, string path)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        Path = path ?? throw new ArgumentNullException(nameof(path));

        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(Path))
        {
            using var _ = File.Create(Path);
        }
    }

    public string SessionId { get; }

    public string Path { get; }

    public event EventHandler<string>? EntryAppended;

    public async Task AppendAsync(
        string kind,
        string message,
        IEnumerable<string>? metadata = null,
        string? body = null,
        bool isBinary = false)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new ArgumentException("Entry kind must be provided.", nameof(kind));
        }

        var timestamp = DateTimeOffset.Now;
        var builder = new StringBuilder();
        builder.Append("----- ")
               .Append(timestamp.ToString("u"))
               .Append(' ')
               .Append(kind.Trim().ToUpperInvariant())
               .AppendLine(" -----");

        if (!string.IsNullOrWhiteSpace(message))
        {
            builder.AppendLine(message.Trim());
        }

        if (metadata is not null)
        {
            foreach (var line in metadata)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    builder.AppendLine(line.Trim());
                }
            }
        }

        if (isBinary)
        {
            builder.AppendLine("*BINARY DATA*");
        }
        else if (!string.IsNullOrWhiteSpace(body))
        {
            builder.AppendLine();
            builder.AppendLine(body);
        }

        builder.AppendLine();

        var entryText = builder.ToString();

        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(Path, entryText).ConfigureAwait(false);
        }
        catch
        {
            // Logging failures should not disrupt the chat experience.
        }
        finally
        {
            _writeLock.Release();
        }

        var trimmed = entryText.TrimEnd();
        try
        {
            EntryAppended?.Invoke(this, trimmed);
        }
        catch
        {
            // Ignore subscriber errors to keep logging resilient.
        }
    }

    public IReadOnlyList<string> ReadRecentEntries(int maxEntries)
    {
        if (maxEntries <= 0)
        {
            return Array.Empty<string>();
        }

        if (!File.Exists(Path))
        {
            return Array.Empty<string>();
        }

        try
        {
            using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var entries = new List<string>();
            var builder = new StringBuilder();

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line is null)
                {
                    continue;
                }

                if (line.StartsWith("----- ", StringComparison.Ordinal) && builder.Length > 0)
                {
                    entries.Add(builder.ToString().TrimEnd());
                    builder.Clear();
                }

                builder.AppendLine(line);
            }

            if (builder.Length > 0)
            {
                entries.Add(builder.ToString().TrimEnd());
            }

            if (entries.Count <= maxEntries)
            {
                return entries;
            }

            return entries.Skip(entries.Count - maxEntries).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
