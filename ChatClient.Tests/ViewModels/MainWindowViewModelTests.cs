using System;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Services;
using ChatClient.ViewModels;
using Xunit;

namespace ChatClient.Tests.ViewModels;

public class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_SeedsWelcomeMessage()
    {
        var viewModel = new MainWindowViewModel(new StubLlmClient("Hello from stub"));

        Assert.Single(viewModel.Messages);
        var message = viewModel.Messages[0];
        Assert.Equal("System", message.Author);
        Assert.Equal("Welcome to Avalonia!", message.Content);
        Assert.False(message.IsUser);
        Assert.True((DateTimeOffset.Now - message.Timestamp) < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Constructor_StartsWithEmptyPrompt()
    {
        var viewModel = new MainWindowViewModel(new StubLlmClient("Hello"));

        Assert.Equal(string.Empty, viewModel.Prompt);
    }

    [Fact]
    public void SendCommand_CannotExecute_WhenPromptIsEmptyOrWhitespace()
    {
        var viewModel = new MainWindowViewModel(new StubLlmClient("Hello"));

        Assert.False(viewModel.SendCommand.CanExecute(null));

        viewModel.Prompt = "   ";

        Assert.False(viewModel.SendCommand.CanExecute(null));
    }

    [Fact]
    public async Task SendCommand_AddsTrimmedUserAndAssistantMessages()
    {
        var viewModel = new MainWindowViewModel(new StubLlmClient("Assistant reply"));
        viewModel.Prompt = "  Hello Avalonia  ";

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, viewModel.Prompt);

        Assert.Equal(3, viewModel.Messages.Count);
        var userMessage = viewModel.Messages[1];
        Assert.Equal("You", userMessage.Author);
        Assert.Equal("Hello Avalonia", userMessage.Content);
        Assert.True(userMessage.IsUser);

        var assistantMessage = viewModel.Messages[2];
        Assert.Equal("Assistant", assistantMessage.Author);
        Assert.Equal("Assistant reply", assistantMessage.Content);
        Assert.False(assistantMessage.IsUser);
    }

    [Fact]
    public async Task SendCommand_IgnoresExecution_WhenPromptIsEmptyAfterTrim()
    {
        var viewModel = new MainWindowViewModel(new StubLlmClient("Hello"));
        viewModel.Prompt = "\t";

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Messages); // still only the welcome message
    }

    [Fact]
    public async Task SendCommand_AppendsSystemMessage_WhenServiceThrows()
    {
        var viewModel = new MainWindowViewModel(new StubLlmClient(new InvalidOperationException("Boom")));
        viewModel.Prompt = "Hi";

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(3, viewModel.Messages.Count);
        var systemMessage = viewModel.Messages[2];
        Assert.Equal("System", systemMessage.Author);
        Assert.Contains("Boom", systemMessage.Content);
        Assert.False(systemMessage.IsUser);
    }

    [Fact]
    public async Task SendCommand_DisablesWhileInFlight()
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new MainWindowViewModel(new AsyncStubLlmClient(() => tcs.Task));
        viewModel.Prompt = "Hello";

        var executionTask = viewModel.SendCommand.ExecuteAsync(null);

        await WaitForAsync(() => viewModel.IsBusy, TimeSpan.FromMilliseconds(200));

        Assert.False(viewModel.SendCommand.CanExecute(null));

        tcs.SetResult("Done");
        await executionTask;

        Assert.False(viewModel.SendCommand.CanExecute(null));
        Assert.False(viewModel.IsBusy);
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

        public Task<string> GetResponseAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(_response ?? string.Empty);
        }
    }

    private sealed class AsyncStubLlmClient : ILlmClient
    {
        private readonly Func<Task<string>> _taskFactory;

        public AsyncStubLlmClient(Func<Task<string>> taskFactory)
        {
            _taskFactory = taskFactory;
        }

        public Task<string> GetResponseAsync(string prompt, CancellationToken cancellationToken = default)
        {
            return _taskFactory();
        }
    }
}
