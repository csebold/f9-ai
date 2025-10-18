using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ChatClient.Models;
using ChatClient.Services;
using ChatClient.ViewModels;
using ChatClient.Views;

namespace ChatClient;

public partial class App : Application
{
    private ISettingsService _settingsService = null!;
    private IModelCatalogService _modelCatalogService = null!;
    private IProjectWorkspaceService _projectWorkspaceService = null!;
    private ISessionPersistenceService _sessionPersistenceService = null!;
    private IBackgroundProcessService _backgroundProcessService = null!;
    private IOllamaProcessManager _ollamaProcessManager = null!;
    private AppSettings _settings = null!;
    private IStartupInitializer _startupInitializer = null!;

    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(15);

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            _settingsService = new SettingsService();
            _modelCatalogService = new ModelCatalogService();
            _projectWorkspaceService = new ProjectWorkspaceService();
            _sessionPersistenceService = new SessionPersistenceService();
            _startupInitializer = new StartupInitializer(_settingsService, _projectWorkspaceService);
            _backgroundProcessService = new BackgroundProcessService();
            _ollamaProcessManager = new OllamaProcessManager(_backgroundProcessService);

            var splashViewModel = new SplashScreenViewModel();
            var splashWindow = new SplashWindow
            {
                DataContext = splashViewModel
            };

            splashViewModel.AddStatus("Starting Foundry-9 AI...");
            splashWindow.Show();

            desktop.Exit += OnDesktopExit;

            _ = InitializeApplicationAsync(desktop, splashWindow, splashViewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    private async Task InitializeApplicationAsync(IClassicDesktopStyleApplicationLifetime desktop, SplashWindow splashWindow, SplashScreenViewModel splashViewModel)
    {
        void PostStatus(string message) =>
            Dispatcher.UIThread.Post(() => splashViewModel.AddStatus(message));

        using var initializationCts = new CancellationTokenSource();
        using var timeoutWatcherCts = new CancellationTokenSource();

        void OnCancelRequested(object? sender, EventArgs e)
        {
            splashViewModel.MarkCancellationInProgress();
            initializationCts.Cancel();
        }

        splashViewModel.CancelRequested += OnCancelRequested;

        Task timeoutTask = Task.CompletedTask;

        try
        {
            var statusProgress = new Progress<string>(PostStatus);
            var initializationTask = _startupInitializer.InitializeAsync(statusProgress, initializationCts.Token);

            timeoutTask = WatchForTimeoutAsync(initializationTask, splashViewModel, timeoutWatcherCts.Token, PostStatus);

            var initializationResult = await initializationTask.ConfigureAwait(false);
            timeoutWatcherCts.Cancel();

            _settings = initializationResult.Settings;
            var activeProject = initializationResult.ActiveProject;
            SessionStoreSnapshot sessionSnapshot;
            if (_settings.EnableSessionPersistence)
            {
                try
                {
                    sessionSnapshot = await _sessionPersistenceService.LoadAsync(initializationCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    PostStatus($"Failed to load chat sessions: {ex.Message}");
                    sessionSnapshot = new SessionStoreSnapshot();
                }
            }
            else
            {
                sessionSnapshot = new SessionStoreSnapshot();
            }

            PostStatus($"Configuring provider: {initializationResult.ProviderDisplayName}...");
            PostStatus("Checking MCP integrations (coming soon)...");
            PostStatus("Finalizing UI...");

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                splashViewModel.ClearCancellationOffer();

                var mainWindowViewModel = new MainWindowViewModel(backgroundProcessService: _backgroundProcessService);
                var mainWindow = new MainWindow(
                    _settingsService,
                    _modelCatalogService,
                    _sessionPersistenceService,
                    _settings,
                    activeProject,
                    sessionSnapshot,
                    _backgroundProcessService,
                    _ollamaProcessManager)
                {
                    DataContext = mainWindowViewModel
                };

                PostStatus("Startup complete.");

                desktop.MainWindow = mainWindow;
                mainWindow.Show();
                splashWindow.Close();
            });
        }
        catch (OperationCanceledException)
        {
            PostStatus("Startup cancelled by user.");
            timeoutWatcherCts.Cancel();
            await Task.Delay(1000, CancellationToken.None).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                splashWindow.Close();
                desktop.Shutdown();
            });
        }
        catch (Exception ex)
        {
            PostStatus($"Startup failed: {ex.Message}");
            timeoutWatcherCts.Cancel();
            await Task.Delay(2000).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                splashWindow.Close();
                desktop.Shutdown();
            });
        }
        finally
        {
            splashViewModel.CancelRequested -= OnCancelRequested;
            timeoutWatcherCts.Cancel();

            try
            {
                await timeoutTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected when the initialization completed in time or the app is shutting down.
            }
        }
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        if (_modelCatalogService is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _ollamaProcessManager?.Dispose();
        _backgroundProcessService?.Dispose();
        OllamaProcessRegistry.Clear();
    }

    private Task WatchForTimeoutAsync(Task initializationTask, SplashScreenViewModel splashViewModel, CancellationToken token, Action<string> postStatus)
    {
        return Task.Run(async () =>
        {
            try
            {
                await Task.Delay(StartupTimeout, token).ConfigureAwait(false);

                if (!initializationTask.IsCompleted)
                {
                    postStatus("Startup is taking longer than expected.");
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        splashViewModel.OfferCancellation("Initialization is taking longer than expected. You can cancel to exit.");
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation; it means initialization completed in time or shutdown was requested.
            }
        });
    }
}
