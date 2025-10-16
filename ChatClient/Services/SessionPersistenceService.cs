using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Models;

namespace ChatClient.Services;

public sealed class SessionPersistenceService : ISessionPersistenceService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _sessionPath;

    public SessionPersistenceService(string? sessionPath = null)
    {
        _sessionPath = string.IsNullOrWhiteSpace(sessionPath)
            ? AppPaths.GetSessionStorePath()
            : sessionPath!;
    }

    public async Task<SessionStoreSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_sessionPath))
        {
            return new SessionStoreSnapshot();
        }

        await using var stream = File.OpenRead(_sessionPath);
        var snapshot = await JsonSerializer.DeserializeAsync<SessionStoreSnapshot>(stream, SerializerOptions, cancellationToken);
        if (snapshot is null)
        {
            return new SessionStoreSnapshot();
        }

        if (snapshot.Version != SessionStoreSnapshot.CurrentVersion)
        {
            snapshot.Version = SessionStoreSnapshot.CurrentVersion;
        }

        return snapshot;
    }

    public async Task SaveAsync(SessionStoreSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        snapshot.Version = SessionStoreSnapshot.CurrentVersion;

        var directory = Path.GetDirectoryName(_sessionPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_sessionPath);
        await JsonSerializer.SerializeAsync(stream, snapshot, SerializerOptions, cancellationToken);
    }

    public Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_sessionPath))
        {
            File.Delete(_sessionPath);
        }

        return Task.CompletedTask;
    }
}
