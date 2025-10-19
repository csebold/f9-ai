using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Models;
using ChatClient.Services;
using ChatClient.ViewModels;
using Xunit;

namespace ChatClient.Tests.ViewModels;

public class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_AddsWelcomeAndProviderMessages()
    {
        var registration = CreateRegistration(new StubLlmClient("Hello"), "TestProvider", "model-a", "Connected to TestProvider.");
        var viewModel = new MainWindowViewModel(registration);

        Assert.Equal(2, viewModel.Messages.Count);
        Assert.Equal("TestProvider", viewModel.CurrentProviderTooltip);

        var welcome = viewModel.Messages[0];
        Assert.Equal("System", welcome.Author);
        Assert.Equal("Welcome to Foundry-9 AI.", welcome.Content);

        var status = viewModel.Messages[1];
        Assert.Equal("System", status.Author);
        Assert.Contains("Using project 'Default Project'.", status.Content);
        Assert.Contains("Connected to TestProvider.", status.Content);
        Assert.Equal("Ready - TestProvider (model-a)", viewModel.StatusMessage);
    }

    [Fact]
    public void Constructor_StartsWithEmptyPrompt()
    {
        var viewModel = new MainWindowViewModel(CreateRegistration(new StubLlmClient("Hello")));

        Assert.Equal(string.Empty, viewModel.Prompt);
    }

    [Fact]
    public void SendCommand_CannotExecute_WhenPromptIsEmptyOrWhitespace()
    {
        var viewModel = new MainWindowViewModel(CreateRegistration(new StubLlmClient("Hello")));

        Assert.False(viewModel.SendCommand.CanExecute(null));

        viewModel.Prompt = "   ";

        Assert.False(viewModel.SendCommand.CanExecute(null));
    }

    [Fact]
    public void RetryCommand_CannotExecute_WhenNoPreviousPrompt()
    {
        var viewModel = new MainWindowViewModel(CreateRegistration(new StubLlmClient("Hello")));

        Assert.False(viewModel.CanRetry);
        Assert.False(viewModel.RetryCommand.CanExecute(null));
    }

    [Fact]
    public async Task SendCommand_AddsTrimmedUserAndAssistantMessages()
    {
        var viewModel = new MainWindowViewModel(CreateRegistration(new StubLlmClient("Assistant reply")));
        viewModel.Prompt = "  Hello Avalonia  ";

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, viewModel.Prompt);

        Assert.Equal(4, viewModel.Messages.Count);
        var userMessage = viewModel.Messages[2];
        Assert.Equal("You", userMessage.Author);
        Assert.Equal("Hello Avalonia", userMessage.Content);
        Assert.True(userMessage.IsUser);

        var assistantMessage = viewModel.Messages[3];
        Assert.Equal("Assistant", assistantMessage.Author);
        Assert.Equal("Assistant reply", assistantMessage.Content);
        Assert.False(assistantMessage.IsUser);
    }

    [Fact]
    public async Task SendCommand_IncludesInstructionsOnFirstMessageOnly()
    {
        var client = new StubLlmClient("Response");
        var viewModel = new MainWindowViewModel(CreateRegistration(client), projectInstructions: "  Follow the workflow. ");

        viewModel.Prompt = "Question";
        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.NotNull(client.LastRequest);
        Assert.True(client.LastRequest!.IncludeInstructions);
        Assert.Equal("Follow the workflow.", client.LastRequest.Instructions);

        viewModel.Prompt = "Second";
        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(2, client.Requests.Count);
        Assert.False(client.Requests[1].IncludeInstructions);

        viewModel.ResetMessages(Array.Empty<Message>(), includeWelcomeWhenEmpty: true);
        viewModel.Prompt = "Third";
        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(3, client.Requests.Count);
        Assert.True(client.Requests[^1].IncludeInstructions);
    }

    [Fact]
    public async Task SendCommand_IgnoresExecution_WhenPromptIsEmptyAfterTrim()
    {
        var viewModel = new MainWindowViewModel(CreateRegistration(new StubLlmClient("Hello")));
        viewModel.Prompt = "\t";

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.Messages.Count);
    }

    [Fact]
    public async Task SendCommand_AppendsSystemMessage_WhenServiceThrows()
    {
        var viewModel = new MainWindowViewModel(CreateRegistration(new StubLlmClient(new InvalidOperationException("Boom"))));
        viewModel.Prompt = "Hi";

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(4, viewModel.Messages.Count);
        var systemMessage = viewModel.Messages[3];
        Assert.Equal("System", systemMessage.Author);
        Assert.Contains("Boom", systemMessage.Content);
        Assert.False(systemMessage.IsUser);
        Assert.True(viewModel.CanRetry);
        Assert.True(viewModel.RetryCommand.CanExecute(null));
        Assert.Contains("Failed after", viewModel.StatusMessage);
        Assert.Contains("Boom", viewModel.ErrorSummary);
    }

    [Fact]
    public async Task SendCommand_DisablesWhileInFlight()
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new MainWindowViewModel(CreateRegistration(new AsyncStubLlmClient((_, _) => tcs.Task)));
        viewModel.Prompt = "Hello";

        var executionTask = viewModel.SendCommand.ExecuteAsync(null);

        await WaitForAsync(() => viewModel.IsResponding, TimeSpan.FromSeconds(1));

        Assert.False(viewModel.SendCommand.CanExecute(null));
        Assert.True(viewModel.StopCommand.CanExecute(null));
        Assert.StartsWith("Requesting response", viewModel.StatusMessage, StringComparison.Ordinal);

        tcs.SetResult("Done");
        await executionTask;

        Assert.False(viewModel.SendCommand.CanExecute(null));
        Assert.False(viewModel.IsResponding);
        Assert.False(viewModel.StopCommand.CanExecute(null));
        Assert.Contains("Responded in", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StopCommand_CancelsInFlightRequest()
    {
        var cancellationObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new MainWindowViewModel(CreateRegistration(new AsyncStubLlmClient((_, ct) =>
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            ct.Register(() =>
            {
                cancellationObserved.TrySetResult(true);
                tcs.TrySetCanceled(ct);
            });
            return tcs.Task;
        })));

        viewModel.Prompt = "Hello";

        var executionTask = viewModel.SendCommand.ExecuteAsync(null);

        await WaitForAsync(() => viewModel.IsResponding, TimeSpan.FromSeconds(1));
        Assert.True(viewModel.StopCommand.CanExecute(null));

        viewModel.StopCommand.Execute(null);

        await executionTask;
        await WaitForAsync(() => !viewModel.IsResponding, TimeSpan.FromSeconds(1));

        Assert.Equal("Request canceled.", viewModel.StatusMessage);
        Assert.False(viewModel.CanRetry);
        Assert.Null(viewModel.ErrorSummary);

        await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task StopCommand_NoopsWhenNotInFlight()
    {
        var viewModel = new MainWindowViewModel(CreateRegistration(new StubLlmClient("Hi")));

        Assert.False(viewModel.StopCommand.CanExecute(null));

        viewModel.StopCommand.Execute(null);

        viewModel.Prompt = "Test";
        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsResponding);
        Assert.Contains("Responded in", viewModel.StatusMessage);
    }

    [Fact]
    public async Task RetryCommand_ReplaysPromptWithoutDuplicatingUserMessage()
    {
        var callCount = 0;
        var viewModel = new MainWindowViewModel(CreateRegistration(new AsyncStubLlmClient((_, _) =>
        {
            callCount++;
            if (callCount == 1)
            {
                return Task.FromException<string>(new InvalidOperationException("Intermittent failure"));
            }

            return Task.FromResult("Recovered");
        })));

        viewModel.Prompt = "Check connectivity";

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.True(viewModel.CanRetry);
        Assert.True(viewModel.RetryCommand.CanExecute(null));
        Assert.Contains("Failed after", viewModel.StatusMessage);
        Assert.Contains("Intermittent failure", viewModel.ErrorSummary);

        await viewModel.RetryCommand.ExecuteAsync(null);

        Assert.False(viewModel.CanRetry);
        Assert.False(viewModel.RetryCommand.CanExecute(null));
        Assert.Contains("Responded in", viewModel.StatusMessage);
        Assert.Null(viewModel.ErrorSummary);
        Assert.Equal(1, viewModel.Messages.Count(message => message.IsUser));
        Assert.Equal(1, viewModel.Messages.Count(message => message.IsAssistant));
    }

    [Fact]
    public void ChangeProject_AppendsStatusMessage()
    {
        var viewModel = new MainWindowViewModel(CreateRegistration(new StubLlmClient("First"), "ProviderA", "model-a"));
        var initialCount = viewModel.Messages.Count;

        var newRegistration = CreateRegistration(new StubLlmClient("Second"), "ProviderB", "model-b", "Connected to ProviderB (model-b).");
        viewModel.ChangeProject(newRegistration, "Project B", "Follow the rules.", "Project overview.", hasCustomProject: true);

        Assert.Equal(initialCount + 1, viewModel.Messages.Count);
        var statusMessage = viewModel.Messages[^1];
        Assert.Equal("System", statusMessage.Author);
        Assert.Contains("Project B", statusMessage.Content);
        Assert.Contains("Project instructions are active.", statusMessage.Content);
        Assert.Contains("Connected to ProviderB (model-b).", statusMessage.Content);
        Assert.Equal("ProviderB", viewModel.CurrentProvider);
        Assert.Equal("model-b", viewModel.CurrentModel);
        Assert.Equal("Project B", viewModel.CurrentProjectName);
        Assert.Equal("Project overview.", viewModel.CurrentDescription);
        Assert.True(viewModel.HasCustomProject);
        Assert.Equal("ProviderB", viewModel.CurrentProviderTooltip);
        Assert.Equal("Ready - ProviderB (model-b)", viewModel.StatusMessage);
    }

    [Fact]
    public void ApplyProviderBranding_UpdatesIconPathAndTooltip()
    {
        var viewModel = new MainWindowViewModel(CreateRegistration(new StubLlmClient("Hello"), "ProviderZ", "model-x"));
        var branding = new ProviderBranding(LlmProvider.OpenAi, "ProviderZ Display", "/tmp/icon.png");

        viewModel.ApplyProviderBranding(branding);

        Assert.Equal("/tmp/icon.png", viewModel.CurrentProviderIconPath);
        Assert.True(viewModel.HasProviderIcon);
        Assert.Equal("ProviderZ Display", viewModel.CurrentProviderTooltip);

        viewModel.ApplyProviderBranding(null);

        Assert.Null(viewModel.CurrentProviderIconPath);
        Assert.False(viewModel.HasProviderIcon);
        Assert.Equal("ProviderZ", viewModel.CurrentProviderTooltip);
    }

    [Fact]
    public void InitializeBackgroundProcesses_PopulatesCollection()
    {
        var service = new StubBackgroundProcessService();
        var snapshot = new BackgroundProcessSnapshot(
            "ollama-daemon",
            "Ollama Server",
            BackgroundProcessCategory.Server,
            "ollama",
            "serve",
            "/tmp/ollama.log",
            DateTimeOffset.UtcNow,
            4242,
            true,
            true,
            null,
            "Running",
            true);

        var viewModel = new MainWindowViewModel(backgroundProcessService: service);
        viewModel.InitializeBackgroundProcesses(service, new[] { snapshot });

        Assert.Single(viewModel.BackgroundProcesses);
        var item = viewModel.BackgroundProcesses[0];
        Assert.Equal("Ollama Server", item.DisplayName);
        Assert.Equal("Running", item.StateText);
    }

    [Fact]
    public void ApplyBackgroundProcessChange_UpdatesExistingItem()
    {
        var service = new StubBackgroundProcessService();
        var start = new BackgroundProcessSnapshot(
            "tool",
            "Local Tool",
            BackgroundProcessCategory.Tool,
            "tool",
            "--serve",
            "/tmp/tool.log",
            DateTimeOffset.UtcNow,
            5252,
            true,
            false,
            null,
            "Starting...",
            true);

        var updated = start with { IsHealthy = true, StatusMessage = "Ready", IsRunning = true };

        var viewModel = new MainWindowViewModel(backgroundProcessService: service);
        viewModel.InitializeBackgroundProcesses(service, new[] { start });

        viewModel.ApplyBackgroundProcessChange(updated, BackgroundProcessChangeKind.Updated);

        Assert.Single(viewModel.BackgroundProcesses);
        var item = viewModel.BackgroundProcesses[0];
        Assert.Equal("Ready", item.StatusMessage);
        Assert.Equal("Running", item.StateText);
    }

    private static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("Condition was not satisfied within the allotted time.");
            }

            await Task.Delay(5);
        }
    }

    private static LlmClientRegistration CreateRegistration(ILlmClient client, string provider = "TestProvider", string model = "test-model", string? statusDetail = null)
        => new(client, LlmProvider.OpenAi, provider, model, statusDetail);

    private sealed class StubBackgroundProcessService : IBackgroundProcessService
    {
        public event EventHandler<BackgroundProcessChangedEventArgs>? ProcessChanged;

        public void Dispose()
        {
        }

        public Task<BackgroundProcessSnapshot> EnsureRunningAsync(BackgroundProcessRequest request, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<BackgroundProcessSnapshot?> GetSnapshotAsync(string id, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public IReadOnlyCollection<BackgroundProcessSnapshot> GetProcesses() => Array.Empty<BackgroundProcessSnapshot>();

        public Task OpenTerminalAsync(string id, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<bool> StopAsync(string id, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private sealed class StubLlmClient : ILlmClient
    {
        private readonly string? _response;
        private readonly Exception? _exception;

        public StubLlmClient(string response)
        {
            _response = response;
        }

        public StubLlmClient(Exception exception)
        {
            _exception = exception;
        }

        public LlmRequest? LastRequest { get; private set; }

        public List<LlmRequest> Requests { get; } = new();

        public Task<string> GetResponseAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            if (_exception is not null)
            {
                throw _exception;
            }

            LastRequest = request ?? throw new ArgumentNullException(nameof(request));
            Requests.Add(request);

            return Task.FromResult(_response ?? string.Empty);
        }
    }

    private sealed class AsyncStubLlmClient : ILlmClient
    {
        private readonly Func<LlmRequest, CancellationToken, Task<string>> _taskFactory;

        public AsyncStubLlmClient(Func<LlmRequest, CancellationToken, Task<string>> taskFactory)
        {
            _taskFactory = taskFactory;
        }

        public Task<string> GetResponseAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            return _taskFactory(request, cancellationToken);
        }
    }
}
