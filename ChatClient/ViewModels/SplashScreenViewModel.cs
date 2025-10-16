using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChatClient.ViewModels;

public partial class SplashScreenViewModel : ObservableObject
{
    public ObservableCollection<string> StatusMessages { get; } = new();

    [ObservableProperty]
    private bool _isCancellationOffered;

    [ObservableProperty]
    private string? _cancellationMessage;

    [ObservableProperty]
    private bool _isCancelEnabled;

    public bool CanCancel => IsCancelEnabled;

    public event EventHandler? CancelRequested;

    public void AddStatus(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var timestamp = DateTimeOffset.Now.ToString("HH:mm:ss");
        StatusMessages.Add($"[{timestamp}] {message}");
    }

    public void OfferCancellation(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        CancellationMessage = message;
        IsCancellationOffered = true;
        IsCancelEnabled = true;
    }

    public void MarkCancellationInProgress()
    {
        CancellationMessage = "Cancelling startup...";
        IsCancelEnabled = false;
    }

    public void ClearCancellationOffer()
    {
        IsCancellationOffered = false;
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsCancellationOfferedChanged(bool value)
    {
        if (!value)
        {
            IsCancelEnabled = false;
            CancellationMessage = null;
        }
    }

    partial void OnIsCancelEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCancel));
        CancelCommand.NotifyCanExecuteChanged();
    }
}
