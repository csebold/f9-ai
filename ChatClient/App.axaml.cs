using System;
using System.Linq;
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
    private AppSettings _settings = null!;

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

        try
        {
            PostStatus("Loading user settings...");
            _settings = await _settingsService.LoadAsync().ConfigureAwait(false);

            var settingsUpdated = false;
            foreach (var project in _settings.Projects)
            {
                var previousPath = project.WorkspacePath;
                ProjectWorkspace.EnsureWorkspace(project);
                if (!string.Equals(previousPath, project.WorkspacePath, StringComparison.Ordinal))
                {
                    settingsUpdated = true;
                }
            }

            var activeProject = ResolveActiveProject(_settings, ref settingsUpdated);
            var providerForStatus = activeProject?.Provider ?? _settings.Provider;
            var providerName = providerForStatus.ToString();
            PostStatus($"Configuring provider: {providerName}...");

            if (settingsUpdated)
            {
                await _settingsService.SaveAsync(_settings).ConfigureAwait(false);
            }

            PostStatus("Checking MCP integrations (coming soon)...");
            PostStatus("Finalizing UI...");

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var mainWindowViewModel = new MainWindowViewModel();
                var mainWindow = new MainWindow(_settingsService, _modelCatalogService, _settings, activeProject)
                {
                    DataContext = mainWindowViewModel
                };

                PostStatus("Startup complete.");

                desktop.MainWindow = mainWindow;
                mainWindow.Show();
                splashWindow.Close();
            });
        }
        catch (Exception ex)
        {
            PostStatus($"Startup failed: {ex.Message}");
            await Task.Delay(2000).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                splashWindow.Close();
                desktop.Shutdown();
            });
        }
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        if (_modelCatalogService is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static ProjectSettings? ResolveActiveProject(AppSettings settings, ref bool settingsUpdated)
    {
        ProjectSettings? activeProject = null;
        if (!string.IsNullOrWhiteSpace(settings.ActiveProjectId))
        {
            activeProject = settings.Projects.FirstOrDefault(p => string.Equals(p.Id, settings.ActiveProjectId, StringComparison.Ordinal));
        }

        if (activeProject is null && settings.Projects.Count > 0)
        {
            activeProject = settings.Projects[0];
            settings.ActiveProjectId = activeProject.Id;
            settingsUpdated = true;
        }

        return activeProject;
    }
}
