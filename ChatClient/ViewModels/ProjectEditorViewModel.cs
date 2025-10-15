using System;
using System.Collections.ObjectModel;
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

    public ProjectEditorViewModel(ProjectSettings project, AppSettings defaults, bool isNewProject)
    {
        _workingCopy = Clone(project ?? throw new ArgumentNullException(nameof(project)));
        _defaults = defaults ?? throw new ArgumentNullException(nameof(defaults));
        _isNewProject = isNewProject;

        Providers = new ObservableCollection<ProjectProviderOption>(BuildProviderOptions(defaults));

        SaveCommand = new RelayCommand(Save, CanSave);
        CancelCommand = new RelayCommand(InvokeCancelled);

        Name = _workingCopy.Name;
        Instructions = _workingCopy.Instructions;
        ApiKey = _workingCopy.ApiKey ?? string.Empty;
        Model = _workingCopy.Model ?? string.Empty;
        WorkspacePath = string.IsNullOrWhiteSpace(_workingCopy.WorkspacePath)
            ? AppPaths.GetProjectWorkspacePath(_workingCopy.Id)
            : _workingCopy.WorkspacePath!;

        SelectedProviderOption = ResolveSelectedProvider(_workingCopy.Provider);
    }

    public ObservableCollection<ProjectProviderOption> Providers { get; }

    public IRelayCommand SaveCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public event EventHandler<ProjectSettings>? Saved;

    public event EventHandler? Cancelled;

    public string Title => _isNewProject ? "Add Project" : "Edit Project";

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _instructions = string.Empty;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _model = string.Empty;

    [ObservableProperty]
    private string _workspacePath = string.Empty;

    [ObservableProperty]
    private ProjectProviderOption _selectedProviderOption;

    private bool CanSave() => !string.IsNullOrWhiteSpace(Name);

    private void Save()
    {
        if (!CanSave())
        {
            return;
        }

        _workingCopy.Name = Name.Trim();
        _workingCopy.Instructions = Instructions?.Trim() ?? string.Empty;
        _workingCopy.Provider = SelectedProviderOption.Provider;
        _workingCopy.ApiKey = string.IsNullOrWhiteSpace(ApiKey) ? null : ApiKey.Trim();
        _workingCopy.Model = string.IsNullOrWhiteSpace(Model) ? null : Model.Trim();
        _workingCopy.WorkspacePath = WorkspacePath;

        Saved?.Invoke(this, Clone(_workingCopy));
    }

    private void InvokeCancelled() => Cancelled?.Invoke(this, EventArgs.Empty);

    partial void OnNameChanged(string value)
    {
        SaveCommand?.NotifyCanExecuteChanged();
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

    private static ProjectProviderOption[] BuildProviderOptions(AppSettings defaults)
    {
        var defaultDisplay = $"Use default ({GetProviderDisplayName(defaults.Provider)})";
        return new[]
        {
            new ProjectProviderOption(null, defaultDisplay),
            new ProjectProviderOption(LlmProvider.OpenAi, "OpenAI"),
            new ProjectProviderOption(LlmProvider.Anthropic, "Anthropic"),
            new ProjectProviderOption(LlmProvider.OpenRouter, "OpenRouter")
        };
    }

    private static string GetProviderDisplayName(LlmProvider provider) =>
        provider switch
        {
            LlmProvider.Anthropic => "Anthropic",
            LlmProvider.OpenRouter => "OpenRouter",
            _ => "OpenAI"
        };

    private static ProjectSettings Clone(ProjectSettings source)
    {
        return new ProjectSettings
        {
            Id = source.Id,
            Name = source.Name,
            Instructions = source.Instructions,
            WorkspacePath = source.WorkspacePath,
            Provider = source.Provider,
            ApiKey = source.ApiKey,
            Model = source.Model,
            ThemeId = source.ThemeId,
            FontFamily = source.FontFamily
        };
    }

    public readonly record struct ProjectProviderOption(LlmProvider? Provider, string DisplayName);
}
