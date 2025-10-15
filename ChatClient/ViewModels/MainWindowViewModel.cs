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
    private ILlmClient _llmClient = null!;
    private LlmClientRegistration _registration = null!;

    public ObservableCollection<Message> Messages { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _prompt = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isBusy;

    public MainWindowViewModel(LlmClientRegistration? registration = null)
    {
        SendCommand = new AsyncRelayCommand(SendAsync, CanSendPrompt);

        AddMessage("System", "Welcome to Foundry-9 AI.", MessageRole.System);

        if (registration is null)
        {
            var fallbackRegistration = CreateFallbackRegistration("LLM provider not configured. Set LLM_PROVIDER and provider-specific API keys.");
            ApplyRegistration(fallbackRegistration);
        }
        else
        {
            ApplyRegistration(registration);
        }
    }

    public IAsyncRelayCommand SendCommand { get; }

    public string CurrentProvider => _registration.ProviderDisplayName;

    public string CurrentModel => _registration.ModelId;

    public void ChangeProvider(LlmClientRegistration registration)
    {
        ApplyRegistration(registration, isUpdate: true);
    }

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

    private void ApplyRegistration(LlmClientRegistration registration, bool isUpdate = false)
    {
        _registration = registration ?? throw new ArgumentNullException(nameof(registration));
        _llmClient = registration.Client;

        var status = registration.StatusDetail;
        if (string.IsNullOrWhiteSpace(status))
        {
            status = isUpdate
                ? $"Switched to {registration.ProviderDisplayName} ({registration.ModelId})."
                : $"Using {registration.ProviderDisplayName} ({registration.ModelId}).";
        }
        else if (isUpdate)
        {
            status = $"Switched LLM provider: {status}";
        }

        AddMessage("System", status, MessageRole.System);
    }

    private void AddMessage(string author, string content, MessageRole role)
    {
        Messages.Add(new Message(author, content, DateTimeOffset.Now, role));
    }
}
