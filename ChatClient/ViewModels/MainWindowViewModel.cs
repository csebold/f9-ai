using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChatClient.Models;

namespace ChatClient.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<Message> Messages { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _prompt = string.Empty;

    public MainWindowViewModel()
    {
        Messages.Add(new Message("System", "Welcome to Avalonia!", DateTimeOffset.Now, false));
    }

    [RelayCommand(CanExecute = nameof(CanSendPrompt))]
    private void Send()
    {
        var trimmed = Prompt.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return;
        }

        Messages.Add(new Message("You", trimmed, DateTimeOffset.Now, true));
        Prompt = string.Empty;
    }

    private bool CanSendPrompt() => !string.IsNullOrWhiteSpace(Prompt);
}
