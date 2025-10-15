using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Models;
using ChatClient.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChatClient.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string DefaultProjectName = "Default Project";

    private ILlmClient _llmClient = null!;
    private LlmClientRegistration _registration = null!;

    public ObservableCollection<Message> Messages { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _prompt = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _currentProjectName = DefaultProjectName;

    [ObservableProperty]
    private string _currentInstructions = string.Empty;

    [ObservableProperty]
    private bool _hasCustomProject;

    public MainWindowViewModel(LlmClientRegistration? registration = null, string? projectName = null, string? projectInstructions = null, bool hasCustomProject = false)
    {
        SendCommand = new AsyncRelayCommand(SendAsync, CanSendPrompt);

        AddMessage("System", "Welcome to Foundry-9 AI.", MessageRole.System);

        var name = string.IsNullOrWhiteSpace(projectName) ? DefaultProjectName : projectName.Trim();
        var instructions = projectInstructions?.Trim() ?? string.Empty;

        if (registration is null)
        {
            var fallbackRegistration = CreateFallbackRegistration("LLM provider not configured. Set LLM_PROVIDER and provider-specific API keys.");
            ApplyContext(fallbackRegistration, name, instructions, hasCustomProject, isUpdate: false, emitStatusMessage: false);
        }
        else
        {
            ApplyContext(registration, name, instructions, hasCustomProject, isUpdate: false, emitStatusMessage: true);
        }
    }

    public IAsyncRelayCommand SendCommand { get; }

    public string CurrentProvider => _registration.ProviderDisplayName;

    public string CurrentModel => _registration.ModelId;

    public void ChangeProject(LlmClientRegistration registration, string projectName, string instructions, bool hasCustomProject, bool isUpdate = true) =>
        ApplyContext(registration, projectName, instructions, hasCustomProject, isUpdate, emitStatusMessage: true);

    private LlmClientRegistration CreateFallbackRegistration(string message)
    {
        return new LlmClientRegistration(new FallbackLlmClient(message), "Unavailable", "N/A", message);
    }

    private bool CanSendPrompt() => !IsBusy && !string.IsNullOrWhiteSpace(Prompt);

    private async Task SendAsync()
    {
        var trimmed = Prompt.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return;
        }

        AddMessage("You", trimmed, MessageRole.User);
        Prompt = string.Empty;

        try
        {
            IsBusy = true;
            var response = await _llmClient.GetResponseAsync(trimmed, CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(response))
            {
                AddMessage("Assistant", response.Trim(), MessageRole.Assistant);
            }
        }
        catch (Exception ex)
        {
            AddMessage("System", $"Error contacting LLM: {ex.Message}", MessageRole.System);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyContext(LlmClientRegistration registration, string projectName, string instructions, bool hasCustomProject, bool isUpdate, bool emitStatusMessage)
    {
        _registration = registration ?? throw new ArgumentNullException(nameof(registration));
        _llmClient = registration.Client;

        CurrentProjectName = string.IsNullOrWhiteSpace(projectName)
            ? DefaultProjectName
            : projectName.Trim();
        CurrentInstructions = instructions?.Trim() ?? string.Empty;
        HasCustomProject = hasCustomProject;

        if (!emitStatusMessage)
        {
            return;
        }

        var statusDetail = registration.StatusDetail;
        if (string.IsNullOrWhiteSpace(statusDetail))
        {
            statusDetail = $"Using {registration.ProviderDisplayName} ({registration.ModelId}).";
        }

        var action = isUpdate ? "Switched to" : "Using";
        var message = $"{action} project '{CurrentProjectName}'. {statusDetail}";
        if (!string.IsNullOrWhiteSpace(CurrentInstructions))
        {
            message += $"{Environment.NewLine}Project instructions are active.";
        }

        AddMessage("System", message, MessageRole.System);
    }

    private void AddMessage(string author, string content, MessageRole role)
    {
        Messages.Add(new Message(author, content, DateTimeOffset.Now, role));
    }
}
