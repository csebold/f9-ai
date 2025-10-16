using System;
using ChatClient.Models;
using Xunit;

namespace ChatClient.Tests.Models;

public class MessageTests
{
    [Fact]
    public void Message_ComputedPropertiesReflectRole()
    {
        var timestamp = DateTimeOffset.UtcNow;

        var userMessage = new Message("User", "Hi", timestamp, MessageRole.User);
        var assistantMessage = new Message("Bot", "Hello", timestamp, MessageRole.Assistant);
        var systemMessage = new Message("System", "Notice", timestamp, MessageRole.System);

        Assert.True(userMessage.IsUser);
        Assert.False(userMessage.IsAssistant);
        Assert.False(userMessage.IsSystem);

        Assert.True(assistantMessage.IsAssistant);
        Assert.False(assistantMessage.IsUser);
        Assert.False(assistantMessage.IsSystem);

        Assert.True(systemMessage.IsSystem);
        Assert.False(systemMessage.IsAssistant);
        Assert.False(systemMessage.IsUser);
    }
}
