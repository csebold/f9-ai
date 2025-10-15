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

    private static LlmClientRegistration CreateLlmRegistration(AppSettings settings)
    {
        try
        {
            return LlmClientFactory.CreateFromSettings(settings);
        }
        catch (Exception ex)
        {
            var detail = $"LLM configuration error: {ex.Message}";
            return new LlmClientRegistration(new FallbackLlmClient(detail), "Unavailable", "N/A", detail);
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

            var providerName = _settings.Provider.ToString();
            PostStatus($"Configuring provider: {providerName}...");

            var registration = CreateLlmRegistration(_settings);

            PostStatus("Checking MCP integrations (coming soon)...");
            PostStatus("Finalizing UI...");

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var mainWindowViewModel = new MainWindowViewModel(registration);
                var mainWindow = new MainWindow(_settingsService, _modelCatalogService, _settings)
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
}
