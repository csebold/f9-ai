using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using ChatClient;
using ChatClient.Models;
using ChatClient.Services;
using ChatClient.ViewModels;
using Xunit;

namespace ChatClient.Tests;

public sealed class AppTests
{
    [Fact]
    public async Task WatchForTimeoutAsync_OffersCancellationAndPostsStatus()
    {
        var app = new App();
        var splashViewModel = new SplashScreenViewModel();
        var statuses = new List<string>();
        var initializationTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeoutOverride = App.OverrideStartupTimeoutForTesting(TimeSpan.FromMilliseconds(10));
        using var uiOverride = App.OverrideUiInvokerForTesting(action =>
        {
            action();
            return Task.CompletedTask;
        });

        using var tokenSource = new CancellationTokenSource();
        var monitorTask = app.WatchForTimeoutAsyncForTesting(initializationTcs.Task, splashViewModel, tokenSource.Token, statuses.Add);

        await monitorTask.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Contains(statuses, status => status.Contains("taking longer", StringComparison.OrdinalIgnoreCase));
        Assert.True(splashViewModel.IsCancellationOffered);
        Assert.Equal("Initialization is taking longer than expected. You can cancel to exit.", splashViewModel.CancellationMessage);
    }

    [Fact]
    public void DisposeServicesForTesting_ShutsDownServicesAndClearsRegistry()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"f9-app-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        using var scope = AppPaths.OverrideBaseDirectoryForTesting(tempDirectory);
        var registryPath = AppPaths.GetOllamaProcessRegistryPath();
        File.WriteAllText(registryPath, "{}");

        var modelCatalog = new StubModelCatalogService();
        var ollamaManager = new StubOllamaProcessManager();
        var backgroundService = new StubBackgroundProcessService();

        var app = new App();
        app.SetServicesForTesting(modelCatalog, ollamaManager, backgroundService);

        app.DisposeServicesForTesting();

        Assert.True(modelCatalog.IsDisposed);
        Assert.True(ollamaManager.IsDisposed);
        Assert.True(backgroundService.IsDisposed);
        Assert.False(File.Exists(registryPath));
    }

    [Fact]
    public void DisableValidationForTesting_RemovesDataAnnotationsPlugins()
    {
        var app = new App();
        var originalPlugins = BindingPlugins.DataValidators
            .OfType<DataAnnotationsValidationPlugin>()
            .ToList();
        var testPlugin = new DataAnnotationsValidationPlugin();
        BindingPlugins.DataValidators.Add(testPlugin);

        try
        {
            Assert.Contains(testPlugin, BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>());

            app.DisableValidationForTesting();

            Assert.DoesNotContain(testPlugin, BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>());
        }
        finally
        {
            foreach (var plugin in BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToList())
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }

            foreach (var plugin in originalPlugins)
            {
                BindingPlugins.DataValidators.Add(plugin);
            }
        }
    }

    private sealed class StubModelCatalogService : IModelCatalogService, IDisposable
    {
        public bool IsDisposed { get; private set; }

        public Task DownloadModelAsync(LlmProvider provider, ProviderSettings providerSettings, string modelId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<ModelCatalogEntry>> GetModelsAsync(LlmProvider provider, ProviderSettings providerSettings, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ModelCatalogEntry>>(Array.Empty<ModelCatalogEntry>());

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class StubOllamaProcessManager : IOllamaProcessManager
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }

        public Task<OllamaProcessEnsureResult> EnsureServerAsync(string? endpoint, CancellationToken cancellationToken) =>
            Task.FromResult(new OllamaProcessEnsureResult(false, null));

        public Task<bool> StopServerAsync(CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private sealed class StubBackgroundProcessService : IBackgroundProcessService
    {
        public bool IsDisposed { get; private set; }

        public event EventHandler<BackgroundProcessChangedEventArgs>? ProcessChanged;

        public void Dispose()
        {
            IsDisposed = true;
        }

        public Task<BackgroundProcessSnapshot> EnsureRunningAsync(BackgroundProcessRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new BackgroundProcessSnapshot(
                "id",
                "name",
                BackgroundProcessCategory.Server,
                "command",
                string.Empty,
                string.Empty,
                DateTimeOffset.UtcNow,
                null,
                true,
                true,
                null,
                null,
                false));

        public IReadOnlyCollection<BackgroundProcessSnapshot> GetProcesses() => Array.Empty<BackgroundProcessSnapshot>();

        public Task<BackgroundProcessSnapshot?> GetSnapshotAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult<BackgroundProcessSnapshot?>(null);

        public Task OpenTerminalAsync(string id, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<bool> StopAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }
}
