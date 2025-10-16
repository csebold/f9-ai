using System;
using System.IO;
using System.Threading.Tasks;
using ChatClient.Models;
using ChatClient.Services;
using Xunit;

namespace ChatClient.Tests.Services;

public class SessionPersistenceServiceTests
{
    [Fact]
    public async Task LoadAsync_ReturnsEmptyWhenFileMissing()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sessions-{Guid.NewGuid():N}.json");
        var service = new SessionPersistenceService(tempFile);

        var snapshot = await service.LoadAsync();

        Assert.NotNull(snapshot);
        Assert.Empty(snapshot.Projects);
        Assert.Equal(SessionStoreSnapshot.CurrentVersion, snapshot.Version);
    }

    [Fact]
    public async Task SaveAsync_PersistsSnapshotToDisk()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"sessions-{Guid.NewGuid():N}");
        var tempFile = Path.Combine(tempDirectory, "sessions.json");
        var service = new SessionPersistenceService(tempFile);

        var snapshot = new SessionStoreSnapshot
        {
            Projects =
            {
                new ProjectSessionSnapshot
                {
                    ProjectId = "alpha",
                    ActiveSessionId = "chat-1",
                    Sessions =
                    {
                        new ChatSessionSnapshot
                        {
                            Id = "chat-1",
                            Title = "Chat 1",
                            Messages =
                            {
                                new Message("User", "Hello", DateTimeOffset.UtcNow, MessageRole.User)
                            }
                        }
                    }
                }
            }
        };

        await service.SaveAsync(snapshot);

        Assert.True(File.Exists(tempFile));

        var reloaded = await service.LoadAsync();
        Assert.Single(reloaded.Projects);
        var project = reloaded.Projects[0];
        Assert.Equal("alpha", project.ProjectId);
        Assert.Equal("chat-1", project.ActiveSessionId);
        Assert.Single(project.Sessions);
        Assert.Single(project.Sessions[0].Messages);
    }

    [Fact]
    public async Task SaveAsync_ThrowsWhenSnapshotNull()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sessions-{Guid.NewGuid():N}.json");
        var service = new SessionPersistenceService(tempFile);

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.SaveAsync(null!));
    }

    [Fact]
    public async Task PurgeAsync_RemovesStoredSnapshot()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"sessions-{Guid.NewGuid():N}");
        var tempFile = Path.Combine(tempDirectory, "sessions.json");
        var service = new SessionPersistenceService(tempFile);

        await service.SaveAsync(new SessionStoreSnapshot());
        Assert.True(File.Exists(tempFile));

        await service.PurgeAsync();

        Assert.False(File.Exists(tempFile));
    }
}
