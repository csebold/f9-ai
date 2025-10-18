using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Models;
using ChatClient.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChatClient.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const string DefaultOllamaEndpoint = "http://localhost:11434/";

    private readonly ISettingsService _settingsService;
    private readonly IModelCatalogService _modelCatalogService;
    private readonly ISessionPersistenceService _sessionPersistenceService;
    private readonly AppSettings _workingCopy;
    private readonly Dictionary<LlmProvider, IReadOnlyList<ModelCatalogEntry>> _modelCache = new();
    private bool _isInitialized;

    public SettingsViewModel(
        ISettingsService settingsService,
        IModelCatalogService modelCatalogService,
        AppSettings settings,
        ISessionPersistenceService sessionPersistenceService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _modelCatalogService = modelCatalogService ?? throw new ArgumentNullException(nameof(modelCatalogService));
        _sessionPersistenceService = sessionPersistenceService ?? throw new ArgumentNullException(nameof(sessionPersistenceService));
        _workingCopy = settings is null ? throw new ArgumentNullException(nameof(settings)) : Clone(settings);

        Providers = new ObservableCollection<ProviderOption>(new[]
        {
            new ProviderOption(LlmProvider.OpenAi, "OpenAI"),
            new ProviderOption(LlmProvider.Anthropic, "Anthropic"),
            new ProviderOption(LlmProvider.OpenRouter, "OpenRouter"),
            new ProviderOption(LlmProvider.Ollama, "Ollama")
        });

        AvailableModelOptions = new ObservableCollection<ModelOption>();
        SendActivationOptions = new ObservableCollection<SendActivationOption>(CreateSendActivationOptions());

        OpenAiApiKey = _workingCopy.OpenAi.ApiKey;
        AnthropicApiKey = _workingCopy.Anthropic.ApiKey;
        OpenRouterApiKey = _workingCopy.OpenRouter.ApiKey;
        if (string.IsNullOrWhiteSpace(_workingCopy.Ollama.Endpoint))
        {
            _workingCopy.Ollama.Endpoint = DefaultOllamaEndpoint;
        }

        OllamaEndpoint = _workingCopy.Ollama.Endpoint;
        EnableSessionPersistence = _workingCopy.EnableSessionPersistence;
        MaxSessionsPerProject = Math.Max(1, _workingCopy.MaxSessionsPerProject);
        MaxMessagesPerSession = Math.Max(1, _workingCopy.MaxMessagesPerSession);

        SelectedProviderOption = Providers.FirstOrDefault(p => p.Provider == _workingCopy.Provider);
        if (SelectedProviderOption == default)
        {
            SelectedProviderOption = Providers[0];
        }

        var initialModelId = _workingCopy.GetProviderSettings(CurrentProvider).Model;
        SelectedModelOption = string.IsNullOrWhiteSpace(initialModelId)
            ? null
            : ModelOption.CreateCustom(initialModelId);

        SelectedSendActivationOption = SendActivationOptions.FirstOrDefault(option => option.Activation == _workingCopy.ChatInput.SendActivation);
        if (SelectedSendActivationOption == default)
        {
            SelectedSendActivationOption = SendActivationOptions[0];
        }

        FetchModelsCommand = new AsyncRelayCommand(FetchModelsAsync, CanFetchModels);
        DownloadModelCommand = new AsyncRelayCommand(DownloadSelectedModelAsync, CanDownloadSelectedModel);
        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        CancelCommand = new RelayCommand(InvokeCancelled);
        PurgeChatsCommand = new AsyncRelayCommand(PurgeChatsAsync, CanPurgeChats);

        ShowOnlyRecommendedModels = true;
        _isInitialized = true;
        LoadModelsFromCache(CurrentProvider);
    }

    public ObservableCollection<ProviderOption> Providers { get; }

    public ObservableCollection<ModelOption> AvailableModelOptions { get; }

    public ObservableCollection<SendActivationOption> SendActivationOptions { get; }

    public event EventHandler<AppSettings>? Saved;

    public event EventHandler? Cancelled;

    [ObservableProperty]
    private ProviderOption _selectedProviderOption;

    [ObservableProperty]
    private ModelOption? _selectedModelOption;

    [ObservableProperty]
    private string _openAiApiKey = string.Empty;

    [ObservableProperty]
    private string _anthropicApiKey = string.Empty;

    [ObservableProperty]
    private string _openRouterApiKey = string.Empty;

    [ObservableProperty]
    private string _ollamaEndpoint = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _enableSessionPersistence;

    [ObservableProperty]
    private double _maxSessionsPerProject;

    [ObservableProperty]
    private double _maxMessagesPerSession;

    [ObservableProperty]
    private SendActivationOption _selectedSendActivationOption;

    [ObservableProperty]
    private bool _showOnlyRecommendedModels = true;

    public IAsyncRelayCommand FetchModelsCommand { get; }

    public IAsyncRelayCommand DownloadModelCommand { get; }

    public IAsyncRelayCommand SaveCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public IAsyncRelayCommand PurgeChatsCommand { get; }

    private LlmProvider CurrentProvider => SelectedProviderOption.Provider;

    private bool CanFetchModels()
    {
        if (IsBusy)
        {
            return false;
        }

        return CurrentProvider switch
        {
            LlmProvider.Ollama => !string.IsNullOrWhiteSpace(OllamaEndpoint),
            _ => !string.IsNullOrWhiteSpace(GetApiKeyForProvider(CurrentProvider))
        };
    }

    private bool CanDownloadSelectedModel()
    {
        if (IsBusy)
        {
            return false;
        }

        if (CurrentProvider != LlmProvider.Ollama)
        {
            return false;
        }

        return SelectedModelOption is { CanDownload: true };
    }

    private bool CanSave()
    {
        if (IsBusy)
        {
            return false;
        }

        var requiresCredentials = CurrentProvider != LlmProvider.Ollama;
        if (requiresCredentials)
        {
            var apiKey = GetApiKeyForProvider(CurrentProvider);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return false;
            }
        }
        else if (string.IsNullOrWhiteSpace(OllamaEndpoint))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(SelectedModelOption?.ModelId);
    }

    private bool CanPurgeChats() => !IsBusy;

    private async Task FetchModelsAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = $"Loading models for {GetProviderDisplayName(CurrentProvider)}...";

            var providerSettings = CreateProviderSettingsSnapshot(CurrentProvider);
            var models = await _modelCatalogService.GetModelsAsync(CurrentProvider, providerSettings, CancellationToken.None);
            _modelCache[CurrentProvider] = models;

            UpdateAvailableModels(models, _workingCopy.GetProviderSettings(CurrentProvider).Model);
            StatusMessage = $"Loaded {models.Count} models.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load models: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            FetchModelsCommand.NotifyCanExecuteChanged();
            DownloadModelCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task DownloadSelectedModelAsync()
    {
        var target = SelectedModelOption;
        if (target is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = $"Downloading {target.ModelId}...";

            var providerSettings = CreateProviderSettingsSnapshot(CurrentProvider);
            await _modelCatalogService.DownloadModelAsync(CurrentProvider, providerSettings, target.ModelId, CancellationToken.None);

            StatusMessage = $"Downloaded {target.ModelId}. Refreshing catalog...";
            var models = await _modelCatalogService.GetModelsAsync(CurrentProvider, providerSettings, CancellationToken.None);
            _modelCache[CurrentProvider] = models;

            UpdateAvailableModels(models, target.ModelId);
            StatusMessage = $"Model {target.ModelId} ready.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to download model: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            FetchModelsCommand.NotifyCanExecuteChanged();
            DownloadModelCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task PurgeChatsAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Purging stored chats...";

            await _sessionPersistenceService.PurgeAsync(CancellationToken.None);
            StatusMessage = "Stored chats purged.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to purge chats: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            PurgeChatsCommand.NotifyCanExecuteChanged();
            FetchModelsCommand.NotifyCanExecuteChanged();
            DownloadModelCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Saving settings...";

            _workingCopy.Provider = CurrentProvider;
            var providerSettings = _workingCopy.GetProviderSettings(CurrentProvider);
            providerSettings.Model = SelectedModelOption?.ModelId ?? string.Empty;
            if (CurrentProvider == LlmProvider.Ollama)
            {
                providerSettings.Endpoint = string.IsNullOrWhiteSpace(OllamaEndpoint)
                    ? DefaultOllamaEndpoint
                    : OllamaEndpoint.Trim();
            }
            _workingCopy.EnableSessionPersistence = EnableSessionPersistence;
            _workingCopy.MaxSessionsPerProject = NormalizeLimit(MaxSessionsPerProject);
            _workingCopy.MaxMessagesPerSession = NormalizeLimit(MaxMessagesPerSession);
            _workingCopy.ChatInput.SendActivation = SelectedSendActivationOption.Activation;

            await _settingsService.SaveAsync(_workingCopy, CancellationToken.None);
            StatusMessage = "Settings saved.";
            Saved?.Invoke(this, Clone(_workingCopy));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save settings: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            SaveCommand.NotifyCanExecuteChanged();
            DownloadModelCommand.NotifyCanExecuteChanged();
        }
    }

    private void InvokeCancelled() => Cancelled?.Invoke(this, EventArgs.Empty);

    private ProviderSettings CreateProviderSettingsSnapshot(LlmProvider provider)
    {
        var working = _workingCopy.GetProviderSettings(provider);
        return new ProviderSettings
        {
            ApiKey = (GetApiKeyForProvider(provider) ?? string.Empty).Trim(),
            Model = SelectedModelOption?.ModelId ?? working.Model ?? string.Empty,
            Endpoint = provider == LlmProvider.Ollama
                ? (OllamaEndpoint ?? string.Empty).Trim()
                : working.Endpoint ?? string.Empty
        };
    }

    private string GetApiKeyForProvider(LlmProvider provider) =>
        provider switch
        {
            LlmProvider.Anthropic => AnthropicApiKey,
            LlmProvider.OpenRouter => OpenRouterApiKey,
            LlmProvider.Ollama => string.Empty,
            _ => OpenAiApiKey
        };

    private void UpdateAvailableModels(IReadOnlyList<ModelCatalogEntry> models, string preferredModel)
    {
        AvailableModelOptions.Clear();

        var filtered = FilterModelsForDisplay(models);
        var installed = filtered.Where(m => m.IsInstalled)
            .OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var others = filtered.Where(m => !m.IsInstalled)
            .OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in installed)
        {
            if (seen.Add(entry.Id))
            {
                AvailableModelOptions.Add(ModelOption.FromEntry(entry));
            }
        }

        foreach (var entry in others)
        {
            if (seen.Add(entry.Id))
            {
                AvailableModelOptions.Add(ModelOption.FromEntry(entry));
            }
        }

        ModelOption? selected = null;
        if (!string.IsNullOrWhiteSpace(preferredModel))
        {
            selected = AvailableModelOptions.FirstOrDefault(option =>
                string.Equals(option.ModelId, preferredModel, StringComparison.OrdinalIgnoreCase));
            if (selected is null)
            {
                selected = ModelOption.CreateCustom(preferredModel);
                AvailableModelOptions.Add(selected);
            }
        }

        if (selected is null && AvailableModelOptions.Count > 0)
        {
            selected = AvailableModelOptions[0];
        }

        SelectedModelOption = selected;
    }

    private void LoadModelsFromCache(LlmProvider provider)
    {
        if (_modelCache.TryGetValue(provider, out var cachedModels))
        {
            UpdateAvailableModels(cachedModels, _workingCopy.GetProviderSettings(provider).Model);
        }
        else
        {
            AvailableModelOptions.Clear();
            var modelId = _workingCopy.GetProviderSettings(provider).Model;
            if (!string.IsNullOrWhiteSpace(modelId))
            {
                var option = ModelOption.CreateCustom(modelId);
                AvailableModelOptions.Add(option);
                SelectedModelOption = option;
            }
            else
            {
                SelectedModelOption = null;
            }
        }
    }

    private IReadOnlyList<ModelCatalogEntry> FilterModelsForDisplay(IReadOnlyList<ModelCatalogEntry> models)
    {
        if (!ShowOnlyRecommendedModels)
        {
            return models;
        }

        return models
            .Where(m => m.IsInstalled || m.IsRecommended)
            .ToArray();
    }

    private static string GetProviderDisplayName(LlmProvider provider) =>
        provider switch
        {
            LlmProvider.Anthropic => "Anthropic",
            LlmProvider.OpenRouter => "OpenRouter",
            LlmProvider.Ollama => "Ollama",
            _ => "OpenAI"
        };

    private static int NormalizeLimit(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 1;
        }

        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return Math.Max(1, rounded);
    }

    private static IReadOnlyList<SendActivationOption> CreateSendActivationOptions()
    {
        return new[]
        {
            new SendActivationOption(ChatSendActivation.Enter, "Enter"),
            new SendActivationOption(ChatSendActivation.ShiftEnter, "Shift + Enter"),
            new SendActivationOption(ChatSendActivation.ControlEnter, "Ctrl + Enter"),
            new SendActivationOption(ChatSendActivation.CommandEnter, "Command + Enter")
        };
    }

    public readonly record struct SendActivationOption(ChatSendActivation Activation, string DisplayName);

    private static AppSettings Clone(AppSettings source)
    {
        return new AppSettings
        {
            Provider = source.Provider,
            OpenAi = new ProviderSettings
            {
                ApiKey = source.OpenAi.ApiKey,
                Model = source.OpenAi.Model,
                Endpoint = source.OpenAi.Endpoint
            },
            Anthropic = new ProviderSettings
            {
                ApiKey = source.Anthropic.ApiKey,
                Model = source.Anthropic.Model,
                Endpoint = source.Anthropic.Endpoint
            },
            OpenRouter = new ProviderSettings
            {
                ApiKey = source.OpenRouter.ApiKey,
                Model = source.OpenRouter.Model,
                Endpoint = source.OpenRouter.Endpoint
            },
            Ollama = new ProviderSettings
            {
                ApiKey = source.Ollama.ApiKey,
                Model = source.Ollama.Model,
                Endpoint = source.Ollama.Endpoint
            },
            ActiveProjectId = source.ActiveProjectId,
            Projects = source.Projects.Select(CloneProject).ToList(),
            EnableSessionPersistence = source.EnableSessionPersistence,
            MaxSessionsPerProject = source.MaxSessionsPerProject,
            MaxMessagesPerSession = source.MaxMessagesPerSession,
            ActiveSessions = source.ActiveSessions?.Count > 0
                ? new Dictionary<string, string>(source.ActiveSessions, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal),
            ChatInput = new ChatInputSettings
            {
                SendActivation = source.ChatInput?.SendActivation ?? ChatSendActivation.Enter
            }
        };
    }

    private static ProjectSettings CloneProject(ProjectSettings source)
    {
        return new ProjectSettings
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description,
            Instructions = source.Instructions,
            WorkspacePath = source.WorkspacePath,
            Provider = source.Provider,
            ApiKey = source.ApiKey,
            Model = source.Model,
            Endpoint = source.Endpoint,
            ThemeId = source.ThemeId,
            FontFamily = source.FontFamily
        };
    }

    partial void OnSelectedProviderOptionChanged(ProviderOption value)
    {
        if (!_isInitialized)
        {
            return;
        }

        LoadModelsFromCache(value.Provider);

        FetchModelsCommand.NotifyCanExecuteChanged();
        DownloadModelCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedModelOptionChanged(ModelOption? value)
    {
        if (!_isInitialized)
        {
            return;
        }

        var modelId = value?.ModelId ?? string.Empty;
        _workingCopy.GetProviderSettings(CurrentProvider).Model = modelId;

        DownloadModelCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedSendActivationOptionChanged(SendActivationOption value)
    {
        if (!_isInitialized)
        {
            return;
        }

        _workingCopy.ChatInput.SendActivation = value.Activation;
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnOpenAiApiKeyChanged(string value)
    {
        if (!_isInitialized)
        {
            return;
        }

        _workingCopy.OpenAi.ApiKey = value ?? string.Empty;
        FetchModelsCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnAnthropicApiKeyChanged(string value)
    {
        if (!_isInitialized)
        {
            return;
        }

        _workingCopy.Anthropic.ApiKey = value ?? string.Empty;
        FetchModelsCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnOpenRouterApiKeyChanged(string value)
    {
        if (!_isInitialized)
        {
            return;
        }

        _workingCopy.OpenRouter.ApiKey = value ?? string.Empty;
        FetchModelsCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnOllamaEndpointChanged(string value)
    {
        if (!_isInitialized)
        {
            return;
        }

        _workingCopy.Ollama.Endpoint = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        FetchModelsCommand.NotifyCanExecuteChanged();
        DownloadModelCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
    }

    partial void OnShowOnlyRecommendedModelsChanged(bool value)
    {
        if (!_isInitialized)
        {
            return;
        }

        if (_modelCache.TryGetValue(CurrentProvider, out var cachedModels))
        {
            UpdateAvailableModels(cachedModels, _workingCopy.GetProviderSettings(CurrentProvider).Model);
        }
        else
        {
            LoadModelsFromCache(CurrentProvider);
        }
    }

    public readonly record struct ProviderOption(LlmProvider Provider, string DisplayName);

    public sealed record ModelOption(string ModelId, string DisplayName, bool IsInstalled, bool IsDownloadable, bool IsRecommended)
    {
        public bool CanDownload => !IsInstalled && IsDownloadable && !string.IsNullOrWhiteSpace(ModelId);

        public static ModelOption FromEntry(ModelCatalogEntry entry)
        {
            var annotations = new List<string>();
            if (entry.IsInstalled)
            {
                annotations.Add("installed");
            }

            if (!entry.IsInstalled && entry.IsDownloadable)
            {
                annotations.Add("available");
            }
            else if (entry.IsInstalled && entry.IsDownloadable)
            {
                annotations.Add("update");
            }

            if (entry.IsRecommended)
            {
                annotations.Add("recommended");
            }

            var display = annotations.Count == 0
                ? entry.Id
                : $"{entry.Id} ({string.Join(", ", annotations)})";

            return new ModelOption(entry.Id, display, entry.IsInstalled, entry.IsDownloadable, entry.IsRecommended);
        }

        public static ModelOption CreateCustom(string modelId)
        {
            var trimmed = modelId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                throw new ArgumentException("Model id must be provided.", nameof(modelId));
            }

            return new ModelOption(trimmed, $"{trimmed} (custom)", false, false, false);
        }
    }
}
