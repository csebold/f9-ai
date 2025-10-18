using System;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChatClient.ViewModels;

public sealed partial class BackgroundProcessItemViewModel : ObservableObject
{
    private readonly IBackgroundProcessService _service;

    public BackgroundProcessItemViewModel(BackgroundProcessSnapshot snapshot, IBackgroundProcessService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));

        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        Id = snapshot.Id;
        Category = snapshot.Category;

        UpdateFromSnapshot(snapshot);
        OpenTerminalCommand = new AsyncRelayCommand(OpenTerminalAsync, CanOpenTerminal);
    }

    public string Id { get; }

    public BackgroundProcessCategory Category { get; }

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isHealthy;

    [ObservableProperty]
    private DateTimeOffset _startedAt;

    [ObservableProperty]
    private string _logPath = string.Empty;

    [ObservableProperty]
    private int? _exitCode;

    [ObservableProperty]
    private bool _managedByApplication;

    public IAsyncRelayCommand OpenTerminalCommand { get; }

    public string Icon => Category switch
    {
        BackgroundProcessCategory.Server => "S",
        BackgroundProcessCategory.Tool => "T",
        BackgroundProcessCategory.Mcp => "M",
        _ => "?"
    };

    public string CategoryDescription => Category switch
    {
        BackgroundProcessCategory.Server => "Server process",
        BackgroundProcessCategory.Tool => "Local tool",
        BackgroundProcessCategory.Mcp => "MCP integration",
        _ => "Background process"
    };

    public string StateText => IsRunning
        ? (IsHealthy ? "Running" : "Starting")
        : "Stopped";

    public void UpdateFromSnapshot(BackgroundProcessSnapshot snapshot)
    {
        DisplayName = snapshot.DisplayName;
        StatusMessage = snapshot.StatusMessage ?? string.Empty;
        IsRunning = snapshot.IsRunning;
        IsHealthy = snapshot.IsHealthy;
        StartedAt = snapshot.StartedAt;
        LogPath = snapshot.LogPath;
        ExitCode = snapshot.ExitCode;
        ManagedByApplication = snapshot.ManagedByApplication;
        OpenTerminalCommand?.NotifyCanExecuteChanged();
    }

    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(StateText));

    partial void OnIsHealthyChanged(bool value) => OnPropertyChanged(nameof(StateText));

    partial void OnManagedByApplicationChanged(bool value) => OpenTerminalCommand?.NotifyCanExecuteChanged();

    private bool CanOpenTerminal() => ManagedByApplication && !string.IsNullOrWhiteSpace(LogPath);

    private async Task OpenTerminalAsync()
    {
        if (!ManagedByApplication)
        {
            return;
        }

        try
        {
            await _service.OpenTerminalAsync(Id, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open terminal: {ex.Message}";
        }
    }
}
