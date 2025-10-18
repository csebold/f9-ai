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

public partial class ProjectEditorViewModel : ObservableObject
{
    private readonly ProjectSettings _workingCopy;
    private readonly AppSettings _defaults;
    private readonly bool _isNewProject;
    private readonly IModelCatalogService _modelCatalogService;
    private readonly Dictionary<LlmProvider, IReadOnlyList<string>> _modelCache = new();
    private bool _isInitialized;
    private LlmProvider _currentProvider;

    public ProjectEditorViewModel(ProjectSettings project, AppSettings defaults, bool isNewProject, IModelCatalogService modelCatalogService)
    {
        _workingCopy = Clone(project ?? throw new ArgumentNullException(nameof(project)));
        _defaults = defaults ?? throw new ArgumentNullException(nameof(defaults));
        _isNewProject = isNewProject;
        _modelCatalogService = modelCatalogService ?? throw new ArgumentNullException(nameof(modelCatalogService));

        Providers = new ObservableCollection<ProjectProviderOption>(BuildProviderOptions(defaults));
        ModelOptions = new ObservableCollection<ModelOption>();

        SaveCommand = new RelayCommand(Save, CanSave);
        CancelCommand = new RelayCommand(InvokeCancelled);
        FetchModelsCommand = new AsyncRelayCommand(FetchModelsAsync, CanFetchModels);

        Name = _workingCopy.Name;
        Description = _workingCopy.Description;
        Instructions = _workingCopy.Instructions;
        ApiKey = _workingCopy.ApiKey ?? string.Empty;
        Model = _workingCopy.Model ?? string.Empty;
        Endpoint = _workingCopy.Endpoint ?? string.Empty;
        WorkspacePath = string.IsNullOrWhiteSpace(_workingCopy.WorkspacePath)
            ? AppPaths.GetProjectWorkspacePath(_workingCopy.Id)
            : _workingCopy.WorkspacePath!;

        SelectedProviderOption = ResolveSelectedProvider(_workingCopy.Provider);
        ResetModelOptions();

        _currentProvider = GetEffectiveProvider();
        _isInitialized = true;

        LoadModelsFromCache(GetEffectiveProvider());
        FetchModelsCommand.NotifyCanExecuteChanged();
    }

    public ObservableCollection<ProjectProviderOption> Providers { get; }

    public ObservableCollection<ModelOption> ModelOptions { get; }

    public IAsyncRelayCommand FetchModelsCommand { get; }

    public IRelayCommand SaveCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public event EventHandler<ProjectSettings>? Saved;

    public event EventHandler? Cancelled;

    public string Title => _isNewProject ? "Add Project" : "Edit Project";

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _instructions = string.Empty;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _model = string.Empty;

    [ObservableProperty]
    private string _endpoint = string.Empty;

    [ObservableProperty]
    private string _workspacePath = string.Empty;

    [ObservableProperty]
    private ProjectProviderOption _selectedProviderOption;

    [ObservableProperty]
    private ModelOption? _selectedModelOption;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    private bool CanSave() => !IsBusy && !string.IsNullOrWhiteSpace(Name);

    private void Save()
    {
        if (!CanSave())
        {
            return;
        }

        _workingCopy.Name = Name.Trim();
        _workingCopy.Description = Description?.Trim() ?? string.Empty;
        _workingCopy.Instructions = Instructions?.Trim() ?? string.Empty;
        _workingCopy.Provider = SelectedProviderOption.Provider;
        _workingCopy.ApiKey = string.IsNullOrWhiteSpace(ApiKey) ? null : ApiKey.Trim();
        _workingCopy.Model = string.IsNullOrWhiteSpace(Model) ? null : Model.Trim();
        _workingCopy.Endpoint = string.IsNullOrWhiteSpace(Endpoint) ? null : Endpoint.Trim();
        _workingCopy.WorkspacePath = WorkspacePath;

        Saved?.Invoke(this, Clone(_workingCopy));
    }

    private void InvokeCancelled() => Cancelled?.Invoke(this, EventArgs.Empty);

    private bool CanFetchModels()
    {
        if (IsBusy)
        {
            return false;
        }

        var provider = GetEffectiveProvider();
        return provider switch
        {
            LlmProvider.Ollama => !string.IsNullOrWhiteSpace(GetEndpointForProvider(provider)),
            _ => !string.IsNullOrWhiteSpace(GetApiKeyForProvider(provider))
        };
    }

    private async Task FetchModelsAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = $"Loading models for {GetProviderDisplayName(GetEffectiveProvider())}...";
            FetchModelsCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();

            var provider = GetEffectiveProvider();
            var providerSettings = CreateProviderSettingsSnapshot(provider);
            var models = await _modelCatalogService.GetModelsAsync(provider, providerSettings, CancellationToken.None);
            _modelCache[provider] = models;

            UpdateModelOptions(models);
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
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    partial void OnNameChanged(string value)
    {
        SaveCommand?.NotifyCanExecuteChanged();
    }

    partial void OnSelectedProviderOptionChanged(ProjectProviderOption value)
    {
        if (!_isInitialized)
        {
            return;
        }

        var newProvider = GetEffectiveProvider();
        if (newProvider != _currentProvider)
        {
            _currentProvider = newProvider;
            Model = string.Empty;
            SelectedModelOption = null;
        }

        StatusMessage = string.Empty;
        RefreshDefaultModelOption();
        LoadModelsFromCache(newProvider);

        FetchModelsCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedModelOptionChanged(ModelOption? value)
    {
        if (!_isInitialized)
        {
            return;
        }

        Model = value?.ModelId ?? string.Empty;
    }

    partial void OnApiKeyChanged(string value)
    {
        if (!_isInitialized)
        {
            return;
        }

        FetchModelsCommand.NotifyCanExecuteChanged();
    }

    partial void OnEndpointChanged(string value)
    {
        if (!_isInitialized)
        {
            return;
        }

        FetchModelsCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        if (!_isInitialized)
        {
            return;
        }

        FetchModelsCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
    }

    private ProjectProviderOption ResolveSelectedProvider(LlmProvider? provider)
    {
        foreach (var option in Providers)
        {
            if (option.Provider == provider)
            {
                return option;
            }
        }

        return Providers[0];
    }

    private void LoadModelsFromCache(LlmProvider provider)
    {
        if (_modelCache.TryGetValue(provider, out var cachedModels))
        {
            UpdateModelOptions(cachedModels);
        }
        else
        {
            ResetModelOptions();
        }
    }

    private void UpdateModelOptions(IReadOnlyList<string> models)
    {
        RefreshDefaultModelOption();

        for (var i = ModelOptions.Count - 1; i >= 1; i--)
        {
            ModelOptions.RemoveAt(i);
        }

        foreach (var model in models)
        {
            ModelOptions.Add(new ModelOption(model, model));
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            SelectedModelOption = ModelOptions[0];
            return;
        }

        var existing = ModelOptions.FirstOrDefault(option =>
            option.ModelId is not null &&
            string.Equals(option.ModelId, Model, StringComparison.OrdinalIgnoreCase));

        if (existing.ModelId is null)
        {
            var customOption = new ModelOption(Model, Model);
            ModelOptions.Add(customOption);
            SelectedModelOption = customOption;
        }
        else
        {
            SelectedModelOption = existing;
        }
    }

    private void ResetModelOptions()
    {
        ModelOptions.Clear();
        ModelOptions.Add(CreateDefaultModelOption());

        if (string.IsNullOrWhiteSpace(Model))
        {
            SelectedModelOption = ModelOptions[0];
            return;
        }

        var customOption = new ModelOption(Model, Model);
        ModelOptions.Add(customOption);
        SelectedModelOption = customOption;
    }

    private void RefreshDefaultModelOption()
    {
        var defaultOption = CreateDefaultModelOption();
        if (ModelOptions.Count == 0)
        {
            ModelOptions.Add(defaultOption);
        }
        else
        {
            ModelOptions[0] = defaultOption;
        }
    }

    private static ProjectProviderOption[] BuildProviderOptions(AppSettings defaults)
    {
        var defaultDisplay = $"Use default ({GetProviderDisplayName(defaults.Provider)})";
        return new[]
        {
            new ProjectProviderOption(null, defaultDisplay),
            new ProjectProviderOption(LlmProvider.OpenAi, "OpenAI"),
            new ProjectProviderOption(LlmProvider.Anthropic, "Anthropic"),
            new ProjectProviderOption(LlmProvider.OpenRouter, "OpenRouter"),
            new ProjectProviderOption(LlmProvider.Ollama, "Ollama")
        };
    }

    private ProviderSettings CreateProviderSettingsSnapshot(LlmProvider provider)
    {
        var apiKey = GetApiKeyForProvider(provider);
        var endpoint = GetEndpointForProvider(provider);

        return new ProviderSettings
        {
            ApiKey = apiKey?.Trim() ?? string.Empty,
            Model = Model ?? string.Empty,
            Endpoint = endpoint?.Trim() ?? string.Empty
        };
    }

    private string? GetApiKeyForProvider(LlmProvider provider)
    {
        if (!string.IsNullOrWhiteSpace(ApiKey))
        {
            return ApiKey;
        }

        return _defaults.GetProviderSettings(provider).ApiKey;
    }

    private string? GetEndpointForProvider(LlmProvider provider)
    {
        if (!string.IsNullOrWhiteSpace(Endpoint))
        {
            return Endpoint;
        }

        return _defaults.GetProviderSettings(provider).Endpoint;
    }

    private LlmProvider GetEffectiveProvider() =>
        SelectedProviderOption.Provider ?? _defaults.Provider;

    private static string GetProviderDisplayName(LlmProvider provider) =>
        provider switch
        {
            LlmProvider.Anthropic => "Anthropic",
            LlmProvider.OpenRouter => "OpenRouter",
            LlmProvider.Ollama => "Ollama",
            _ => "OpenAI"
        };

    private ModelOption CreateDefaultModelOption()
    {
        var providerDisplay = GetProviderDisplayName(GetEffectiveProvider());
        return new ModelOption(null, $"Use default ({providerDisplay})");
    }

    private static ProjectSettings Clone(ProjectSettings source)
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

    public readonly record struct ProjectProviderOption(LlmProvider? Provider, string DisplayName);

    public readonly record struct ModelOption(string? ModelId, string DisplayName);
}
