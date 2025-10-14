using System;
using ChatClient.ViewModels;
using Xunit;

namespace ChatClient.Tests.ViewModels;

public class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_SeedsWelcomeMessage()
    {
        var viewModel = new MainWindowViewModel();

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
        var viewModel = new MainWindowViewModel();

        Assert.Equal(string.Empty, viewModel.Prompt);
    }

    [Fact]
    public void SendCommand_CannotExecute_WhenPromptIsEmptyOrWhitespace()
    {
        var viewModel = new MainWindowViewModel();

        Assert.False(viewModel.SendCommand.CanExecute(null));

        viewModel.Prompt = "   ";

        Assert.False(viewModel.SendCommand.CanExecute(null));
    }

    [Fact]
    public void SendCommand_AddsTrimmedUserMessageAndClearsPrompt()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.Prompt = "  Hello Avalonia  ";

        Assert.True(viewModel.SendCommand.CanExecute(null));

        viewModel.SendCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.Prompt);
        Assert.False(viewModel.SendCommand.CanExecute(null));

        Assert.Equal(2, viewModel.Messages.Count);
        var userMessage = viewModel.Messages[1];
        Assert.Equal("You", userMessage.Author);
        Assert.Equal("Hello Avalonia", userMessage.Content);
        Assert.True(userMessage.IsUser);
    }

    [Fact]
    public void SendCommand_IgnoresExecution_WhenPromptIsEmptyAfterTrim()
    {
        var viewModel = new MainWindowViewModel
        {
            Prompt = "\t"
        };

        viewModel.SendCommand.Execute(null);

        Assert.Single(viewModel.Messages); // still only the welcome message
    }
}
