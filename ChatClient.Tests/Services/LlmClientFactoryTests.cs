using ChatClient.Models;
using ChatClient.Services;
using Xunit;

namespace ChatClient.Tests.Services;

public class LlmClientFactoryTests
{
    [Fact]
    public void CreateForProject_DefaultsToAppSettingsWhenProjectMissing()
    {
        var settings = CreateDefaultSettings();

        var registration = LlmClientFactory.CreateForProject(settings, project: null);

        Assert.Equal("OpenAI", registration.ProviderDisplayName);
        Assert.Equal("gpt-4o-mini", registration.ModelId);
    }

    [Fact]
    public void CreateForProject_UsesProjectOverridesForSameProvider()
    {
        var settings = CreateDefaultSettings();
        var project = new ProjectSettings
        {
            Name = "Custom",
            ApiKey = "project-openai-key",
            Model = "gpt-4o-custom"
        };

        var registration = LlmClientFactory.CreateForProject(settings, project);

        Assert.Equal("OpenAI", registration.ProviderDisplayName);
        Assert.Equal("gpt-4o-custom", registration.ModelId);
    }

    [Fact]
    public void CreateForProject_UsesProjectProviderWhenSpecified()
    {
        var settings = CreateDefaultSettings();
        settings.Anthropic.ApiKey = "anthropic-global";
        settings.Anthropic.Model = "claude-3-haiku";

        var project = new ProjectSettings
        {
            Provider = LlmProvider.Anthropic,
            ApiKey = "anthropic-project",
            Model = "claude-3-sonnet"
        };

        var registration = LlmClientFactory.CreateForProject(settings, project);

        Assert.Equal("Anthropic", registration.ProviderDisplayName);
        Assert.Equal("claude-3-sonnet", registration.ModelId);
    }

    private static AppSettings CreateDefaultSettings()
    {
        return new AppSettings
        {
            Provider = LlmProvider.OpenAi,
            OpenAi = new ProviderSettings
            {
                ApiKey = "openai-default",
                Model = "gpt-4o-mini"
            },
            Anthropic = new ProviderSettings
            {
                ApiKey = "anthropic-default",
                Model = "claude-3-haiku"
            },
            OpenRouter = new ProviderSettings
            {
                ApiKey = "openrouter-default",
                Model = "openrouter/auto"
            }
        };
    }
}
