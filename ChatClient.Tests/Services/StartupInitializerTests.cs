using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Models;
using ChatClient.Services;
using Xunit;

namespace ChatClient.Tests.Services;

public class StartupInitializerTests
{
    [Fact]
    public async Task InitializeAsync_CompletesAndPersistsChangesWhenWorkspaceUpdated()
    {
        var settings = new AppSettings
        {
            Provider = LlmProvider.OpenAi,
            Projects =
            {
                new ProjectSettings { Id = "alpha", Name = "Alpha" },
                new ProjectSettings { Id = "beta", Name = "Beta", Provider = LlmProvider.Anthropic }
            }
        };

        var settingsService = new FakeSettingsService(settings);
        var workspaceService = new TrackingWorkspaceService(new Dictionary<string, bool>
        {
            ["alpha"] = true,
            ["beta"] = false
        });

        var brandingService = new RecordingBrandingService();
        var initializer = new StartupInitializer(settingsService, workspaceService, brandingService);

        var progressMessages = new List<string>();
        var result = await initializer.InitializeAsync(
            new Progress<string>(progressMessages.Add),
            CancellationToken.None);

        Assert.Equal(1, settingsService.LoadCallCount);
        Assert.Equal(1, settingsService.SaveCallCount);
        Assert.Same(settings, result.Settings);
        Assert.Equal("OpenAi", result.ProviderDisplayName);
        Assert.Equal("alpha", result.Settings.ActiveProjectId);
        Assert.Same(settings.Projects[0], result.ActiveProject);
        Assert.Contains("Loading user settings...", progressMessages);
        Assert.Contains("Preparing workspace for 'Alpha'...", progressMessages);
        Assert.Contains("Selecting active project...", progressMessages);
        Assert.Contains("Refreshing provider branding...", progressMessages);
        Assert.Contains("Saving updated settings...", progressMessages);
        Assert.Equal(new[] { settings.Projects[0], settings.Projects[1] }, workspaceService.EnsuredProjects);
        Assert.Equal(1, brandingService.RefreshAllCallCount);
    }

    [Fact]
    public async Task InitializeAsync_DoesNotPersistWhenNoChangesDetected()
    {
        var project = new ProjectSettings { Id = "gamma", Name = "Gamma" };
        var settings = new AppSettings
        {
            Provider = LlmProvider.OpenRouter,
            ActiveProjectId = project.Id,
            Projects = { project }
        };

        var settingsService = new FakeSettingsService(settings);
        var workspaceService = new TrackingWorkspaceService(new Dictionary<string, bool>());
        var brandingService = new RecordingBrandingService();
        var initializer = new StartupInitializer(settingsService, workspaceService, brandingService);

        var result = await initializer.InitializeAsync(
            new Progress<string>(_ => { }),
            CancellationToken.None);

        Assert.Equal(1, settingsService.LoadCallCount);
        Assert.Equal(0, settingsService.SaveCallCount);
        Assert.Equal("OpenRouter", result.ProviderDisplayName);
        Assert.Same(project, result.ActiveProject);
        Assert.Equal(1, brandingService.RefreshAllCallCount);
    }

    [Fact]
    public async Task InitializeAsync_ThrowsWhenCancelled()
    {
        var settings = new AppSettings();
        var settingsService = new FakeSettingsService(settings);
        var workspaceService = new TrackingWorkspaceService(new Dictionary<string, bool>());
        var brandingService = new RecordingBrandingService();
        var initializer = new StartupInitializer(settingsService, workspaceService, brandingService);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            initializer.InitializeAsync(new Progress<string>(_ => { }), cts.Token));

        Assert.Empty(workspaceService.EnsuredProjects);
        Assert.Equal(0, settingsService.SaveCallCount);
        Assert.Equal(0, brandingService.RefreshAllCallCount);
    }

    [Fact]
    public async Task InitializeAsync_UsesActiveProjectProviderWhenSpecified()
    {
        var project = new ProjectSettings
        {
            Id = "omega",
            Name = "Omega",
            Provider = LlmProvider.Anthropic
        };

        var settings = new AppSettings
        {
            Provider = LlmProvider.OpenAi,
            ActiveProjectId = project.Id,
            Projects = { project }
        };

        var settingsService = new FakeSettingsService(settings);
        var workspaceService = new TrackingWorkspaceService(new Dictionary<string, bool>());
        var brandingService = new RecordingBrandingService();
        var initializer = new StartupInitializer(settingsService, workspaceService, brandingService);

        var result = await initializer.InitializeAsync(new Progress<string>(_ => { }), CancellationToken.None);

        Assert.Equal("Anthropic", result.ProviderDisplayName);
        Assert.Same(project, result.ActiveProject);
        Assert.Equal(1, brandingService.RefreshAllCallCount);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        private readonly AppSettings _settings;

        public FakeSettingsService(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public int LoadCallCount { get; private set; }

        public int SaveCallCount { get; private set; }

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadCallCount++;
            return Task.FromResult(_settings);
        }

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingWorkspaceService : IProjectWorkspaceService
    {
        private readonly IReadOnlyDictionary<string, bool> _changes;

        public TrackingWorkspaceService(IReadOnlyDictionary<string, bool> changes)
        {
            _changes = changes;
        }

        public List<ProjectSettings> EnsuredProjects { get; } = new();

        public bool EnsureWorkspace(ProjectSettings project)
        {
            EnsuredProjects.Add(project);
            return project is not null &&
                   _changes.TryGetValue(project.Id, out var updated) &&
                   updated;
        }
    }

    private sealed class RecordingBrandingService : IProviderBrandingService
    {
        public int RefreshAllCallCount { get; private set; }

        public Task<ProviderBranding> GetBrandingAsync(LlmProvider provider, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProviderBranding(provider, provider.ToString(), null));

        public Task<IReadOnlyDictionary<LlmProvider, ProviderBranding>> RefreshAllAsync(CancellationToken cancellationToken = default)
        {
            RefreshAllCallCount++;
            IReadOnlyDictionary<LlmProvider, ProviderBranding> snapshot = new Dictionary<LlmProvider, ProviderBranding>();
            return Task.FromResult(snapshot);
        }

        public Task<ProviderBranding> RefreshAsync(LlmProvider provider, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProviderBranding(provider, provider.ToString(), null));
    }
}
