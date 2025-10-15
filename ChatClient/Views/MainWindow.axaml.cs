using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ChatClient.Models;
using ChatClient.Services;
using ChatClient.ViewModels;

namespace ChatClient.Views;

public partial class MainWindow : Window
{
    private readonly ISettingsService _settingsService;
    private readonly IModelCatalogService _modelCatalogService;
    private AppSettings _settings;

    public MainWindow()
        : this(new SettingsService(), new ModelCatalogService(), new AppSettings())
    {
    }

    public MainWindow(ISettingsService settingsService, IModelCatalogService modelCatalogService, AppSettings settings)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _modelCatalogService = modelCatalogService ?? throw new ArgumentNullException(nameof(modelCatalogService));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        InitializeComponent();
    }

    private async void SettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var dialog = new SettingsWindow
        {
            DataContext = new SettingsViewModel(_settingsService, _modelCatalogService, _settings)
        };

        var result = await ShowSettingsDialogAsync(dialog);
        if (result is null)
        {
            return;
        }

        _settings = result;
        ApplySettings(viewModel, _settings);
    }

    private async Task<AppSettings?> ShowSettingsDialogAsync(SettingsWindow dialog)
    {
        try
        {
            return await dialog.ShowDialog<AppSettings?>(this);
        }
        catch
        {
            return null;
        }
    }

    private static void ApplySettings(MainWindowViewModel viewModel, AppSettings settings)
    {
        try
        {
            var registration = LlmClientFactory.CreateFromSettings(settings);
            viewModel.ChangeProvider(registration);
        }
        catch (Exception ex)
        {
            var detail = $"LLM configuration error: {ex.Message}";
            var fallback = new LlmClientRegistration(new FallbackLlmClient(detail), "Unavailable", "N/A", detail);
            viewModel.ChangeProvider(fallback);
        }
    }
}
