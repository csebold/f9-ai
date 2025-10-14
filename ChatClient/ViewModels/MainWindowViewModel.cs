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
    private readonly ILlmClient _llmClient;

    public ObservableCollection<Message> Messages { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _prompt = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isBusy;

    public MainWindowViewModel(ILlmClient? llmClient = null)
    {
        _llmClient = llmClient ?? new FallbackLlmClient("LLM provider not configured. Set LLM_PROVIDER and provider-specific API keys.");

        Messages.Add(new Message("System", "Welcome to Avalonia!", DateTimeOffset.Now, false));

        SendCommand = new AsyncRelayCommand(SendAsync, CanSendPrompt);
    }

    public IAsyncRelayCommand SendCommand { get; }

    private bool CanSendPrompt() => !IsBusy && !string.IsNullOrWhiteSpace(Prompt);

    private async Task SendAsync()
    {
        var trimmed = Prompt.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return;
        }

        AddMessage("You", trimmed, isUser: true);
        Prompt = string.Empty;

        try
        {
            IsBusy = true;
            var response = await _llmClient.GetResponseAsync(trimmed, CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(response))
            {
                AddMessage("Assistant", response.Trim(), isUser: false);
            }
        }
        catch (Exception ex)
        {
            AddMessage("System", $"Error contacting LLM: {ex.Message}", isUser: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AddMessage(string author, string content, bool isUser)
    {
        Messages.Add(new Message(author, content, DateTimeOffset.Now, isUser));
    }
}
