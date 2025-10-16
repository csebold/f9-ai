using System;
using System.IO;
using System.Threading.Tasks;
using ChatClient.Models;
using ChatClient.Services;
using Xunit;

namespace ChatClient.Tests.Services;

public class SettingsServiceTests
{
    [Fact]
    public async Task LoadAsync_ReturnsNewSettingsWhenFileMissing()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var service = new SettingsService(tempFile);

        var settings = await service.LoadAsync();

        Assert.NotNull(settings);
        Assert.Empty(settings.Projects);
    }

    [Fact]
    public async Task SaveAsync_PersistsSettingsToDisk()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"settings-{Guid.NewGuid():N}");
        var tempFile = Path.Combine(tempDirectory, "settings.json");
        var service = new SettingsService(tempFile);

        var settings = new AppSettings
        {
            ActiveProjectId = "project-1",
            Projects =
            {
                new ProjectSettings { Id = "project-1", Name = "Project One" }
            },
            EnableSessionPersistence = false,
            MaxSessionsPerProject = 5,
            MaxMessagesPerSession = 75,
            ActiveSessions = { ["project-1"] = "session-9" }
        };

        await service.SaveAsync(settings);

        Assert.True(File.Exists(tempFile));

        var reloaded = await service.LoadAsync();
        Assert.Equal("project-1", reloaded.ActiveProjectId);
        Assert.Single(reloaded.Projects);
        Assert.Equal("Project One", reloaded.Projects[0].Name);
        Assert.False(reloaded.EnableSessionPersistence);
        Assert.Equal(5, reloaded.MaxSessionsPerProject);
        Assert.Equal(75, reloaded.MaxMessagesPerSession);
        Assert.Equal("session-9", reloaded.ActiveSessions["project-1"]);
    }

    [Fact]
    public async Task SaveAsync_ThrowsWhenSettingsNull()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var service = new SettingsService(tempFile);

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.SaveAsync(null!));
    }
}
