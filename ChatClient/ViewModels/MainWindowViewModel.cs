using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    private bool _isResponding;

    [ObservableProperty]
    private string _currentProjectName = DefaultProjectName;

    [ObservableProperty]
    private string _currentInstructions = string.Empty;

    [ObservableProperty]
    private string _currentDescription = string.Empty;

    [ObservableProperty]
    private bool _hasCustomProject;

    [ObservableProperty]
    private string _statusMessage = "Ready.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RetryCommand))]
    private bool _canRetry;

    [ObservableProperty]
    private string? _errorSummary;

    private CancellationTokenSource? _responseCancellation;
    private string? _lastPrompt;

    public MainWindowViewModel(
        LlmClientRegistration? registration = null,
        string? projectName = null,
        string? projectInstructions = null,
        string? projectDescription = null,
        bool hasCustomProject = false,
        IEnumerable<Message>? initialMessages = null)
    {
        SendCommand = new AsyncRelayCommand(SendAsync, CanSendPrompt);
        StopCommand = new RelayCommand(StopRequest, CanStopRequest);
        RetryCommand = new AsyncRelayCommand(RetryAsync, CanRetryRequest);

        ResetMessages(initialMessages, includeWelcomeWhenEmpty: true);

        var name = string.IsNullOrWhiteSpace(projectName) ? DefaultProjectName : projectName.Trim();
        var instructions = projectInstructions?.Trim() ?? string.Empty;
        var description = projectDescription?.Trim() ?? string.Empty;

        if (registration is null)
        {
            var fallbackRegistration = CreateFallbackRegistration("LLM provider not configured. Set LLM_PROVIDER and provider-specific API keys.");
            ApplyContext(fallbackRegistration, name, instructions, description, hasCustomProject, isUpdate: false, emitStatusMessage: false);
        }
        else
        {
            ApplyContext(registration, name, instructions, description, hasCustomProject, isUpdate: false, emitStatusMessage: true);
        }
    }

    public IAsyncRelayCommand SendCommand { get; }

    public IRelayCommand StopCommand { get; }

    public IAsyncRelayCommand RetryCommand { get; }

    public string CurrentProvider => _registration.ProviderDisplayName;

    public string CurrentModel => _registration.ModelId;

    public void ChangeProject(
        LlmClientRegistration registration,
        string projectName,
        string instructions,
        string description,
        bool hasCustomProject,
        bool isUpdate = true,
        bool emitStatusMessage = true) =>
        ApplyContext(registration, projectName, instructions, description, hasCustomProject, isUpdate, emitStatusMessage);

    private LlmClientRegistration CreateFallbackRegistration(string message)
    {
        return new LlmClientRegistration(new FallbackLlmClient(message), "Unavailable", "N/A", message);
    }

    private bool CanSendPrompt() => !IsResponding && !string.IsNullOrWhiteSpace(Prompt);

    private bool CanStopRequest() => IsResponding;

    private bool CanRetryRequest() => CanRetry && !IsResponding;

    private async Task SendAsync()
    {
        var trimmed = Prompt.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return;
        }

        Prompt = string.Empty;

        await SendCoreAsync(trimmed, appendUserMessage: true);
    }

    private async Task RetryAsync()
    {
        if (string.IsNullOrWhiteSpace(_lastPrompt))
        {
            return;
        }

        await SendCoreAsync(_lastPrompt, appendUserMessage: false);
    }

    private async Task SendCoreAsync(string prompt, bool appendUserMessage)
    {
        _lastPrompt = prompt;
        ErrorSummary = null;
        CanRetry = false;

        if (appendUserMessage)
        {
            AddMessage("You", prompt, MessageRole.User);
        }

        var cancellation = new CancellationTokenSource();
        _responseCancellation = cancellation;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            IsResponding = true;
            StatusMessage = $"Requesting response from {CurrentProvider} ({CurrentModel})...";

            var response = await _llmClient.GetResponseAsync(prompt, cancellation.Token);

            stopwatch.Stop();

            if (!string.IsNullOrWhiteSpace(response))
            {
                AddMessage("Assistant", response.Trim(), MessageRole.Assistant);
            }

            StatusMessage = $"Responded in {FormatLatency(stopwatch.Elapsed)} via {CurrentProvider} ({CurrentModel}).";
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            StatusMessage = "Request canceled.";
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            StatusMessage = $"Failed after {FormatLatency(stopwatch.Elapsed)} contacting {CurrentProvider} ({CurrentModel}).";
            ErrorSummary = ex.Message;
            CanRetry = true;
            AddMessage("System", $"Error contacting LLM: {ex.Message}", MessageRole.System);
        }
        finally
        {
            cancellation.Dispose();
            if (ReferenceEquals(_responseCancellation, cancellation))
            {
                _responseCancellation = null;
            }

            IsResponding = false;
        }
    }

    private void StopRequest()
    {
        if (!IsResponding)
        {
            return;
        }

        _responseCancellation?.Cancel();
        StatusMessage = $"Canceling request to {CurrentProvider}...";
    }

    private static string FormatLatency(TimeSpan duration)
    {
        if (duration.TotalMilliseconds < 1000)
        {
            return $"{duration.TotalMilliseconds:F0} ms";
        }

        return $"{duration.TotalSeconds:F1} s";
    }

    public void ResetMessages(IEnumerable<Message>? messages, bool includeWelcomeWhenEmpty)
    {
        Messages.Clear();

        if (messages is not null)
        {
            foreach (var message in messages)
            {
                if (message is not null)
                {
                    Messages.Add(message);
                }
            }
        }

        if (includeWelcomeWhenEmpty && Messages.Count == 0)
        {
            AddMessage("System", "Welcome to Foundry-9 AI.", MessageRole.System);
        }
    }

    private void ApplyContext(LlmClientRegistration registration, string projectName, string instructions, string description, bool hasCustomProject, bool isUpdate, bool emitStatusMessage)
    {
        _registration = registration ?? throw new ArgumentNullException(nameof(registration));
        _llmClient = registration.Client;

        CurrentProjectName = string.IsNullOrWhiteSpace(projectName)
            ? DefaultProjectName
            : projectName.Trim();
        CurrentInstructions = instructions?.Trim() ?? string.Empty;
        CurrentDescription = description?.Trim() ?? string.Empty;
        HasCustomProject = hasCustomProject;

        StatusMessage = $"Ready - {registration.ProviderDisplayName} ({registration.ModelId})";

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
