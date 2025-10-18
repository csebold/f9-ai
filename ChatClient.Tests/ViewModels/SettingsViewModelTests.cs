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

public class SettingsViewModelTests
{
    [Fact]
    public async Task SaveCommand_PersistsSettingsAndRaisesSaved()
    {
        var settings = new AppSettings
        {
            Provider = LlmProvider.OpenAi,
            OpenAi = new ProviderSettings { ApiKey = "openai-key", Model = "gpt-4o-mini" },
            OpenRouter = new ProviderSettings { ApiKey = "router-key", Model = "openrouter/auto" }
        };

        var settingsService = new RecordingSettingsService();
        var modelCatalogService = new StubModelCatalogService();
        var sessionService = new StubSessionPersistenceService();
        var viewModel = new SettingsViewModel(settingsService, modelCatalogService, settings, sessionService);

        AppSettings? savedResult = null;
        viewModel.Saved += (_, updated) => savedResult = updated;

        var openRouterOption = viewModel.Providers.First(p => p.Provider == LlmProvider.OpenRouter);
        viewModel.SelectedProviderOption = openRouterOption;
        viewModel.OpenRouterApiKey = "new-router-key";
        viewModel.SelectedModel = "openrouter/test-model";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(settingsService.LastSaved);
        Assert.Equal(LlmProvider.OpenRouter, settingsService.LastSaved.Provider);
        Assert.Equal("new-router-key", settingsService.LastSaved.OpenRouter.ApiKey);
        Assert.Equal("openrouter/test-model", settingsService.LastSaved.OpenRouter.Model);
        Assert.Equal(savedResult?.OpenRouter.Model, settingsService.LastSaved.OpenRouter.Model);
    }

    [Fact]
    public async Task SaveCommand_PersistsSessionSettings()
    {
        var settings = new AppSettings
        {
            Provider = LlmProvider.OpenAi,
            OpenAi = new ProviderSettings { ApiKey = "openai-key", Model = "gpt-4o-mini" },
            EnableSessionPersistence = true,
            MaxSessionsPerProject = 5,
            MaxMessagesPerSession = 150
        };

        var settingsService = new RecordingSettingsService();
        var modelCatalogService = new StubModelCatalogService();
        var sessionService = new StubSessionPersistenceService();
        var viewModel = new SettingsViewModel(settingsService, modelCatalogService, settings, sessionService);

        viewModel.EnableSessionPersistence = false;
        viewModel.MaxSessionsPerProject = 3;
        viewModel.MaxMessagesPerSession = 42;
        viewModel.SelectedModel = "gpt-4o-mini";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(settingsService.LastSaved);
        Assert.False(settingsService.LastSaved!.EnableSessionPersistence);
        Assert.Equal(3, settingsService.LastSaved.MaxSessionsPerProject);
        Assert.Equal(42, settingsService.LastSaved.MaxMessagesPerSession);
    }

    [Fact]
    public async Task SaveCommand_PersistsOllamaSettings()
    {
        var settings = new AppSettings
        {
            Provider = LlmProvider.Ollama,
            Ollama = new ProviderSettings { Endpoint = "http://localhost:11434/", Model = "llama3" }
        };

        var settingsService = new RecordingSettingsService();
        var modelCatalogService = new StubModelCatalogService(new[] { "llama3", "llama3.1" });
        var sessionService = new StubSessionPersistenceService();
        var viewModel = new SettingsViewModel(settingsService, modelCatalogService, settings, sessionService);

        var ollamaOption = viewModel.Providers.First(p => p.Provider == LlmProvider.Ollama);
        viewModel.SelectedProviderOption = ollamaOption;
        viewModel.OllamaEndpoint = "http://localhost:12345/";
        viewModel.SelectedModel = "llama3.1";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(settingsService.LastSaved);
        Assert.Equal(LlmProvider.Ollama, settingsService.LastSaved.Provider);
        Assert.Equal("http://localhost:12345/", settingsService.LastSaved.Ollama.Endpoint);
        Assert.Equal("llama3.1", settingsService.LastSaved.Ollama.Model);
    }

    [Fact]
    public void SaveCommand_RequiresOllamaEndpoint()
    {
        var settings = new AppSettings
        {
            Provider = LlmProvider.Ollama,
            Ollama = new ProviderSettings { Endpoint = "http://localhost:11434/", Model = "llama3" }
        };

        var settingsService = new RecordingSettingsService();
        var modelCatalogService = new StubModelCatalogService(new[] { "llama3" });
        var sessionService = new StubSessionPersistenceService();
        var viewModel = new SettingsViewModel(settingsService, modelCatalogService, settings, sessionService);

        var ollamaOption = viewModel.Providers.First(p => p.Provider == LlmProvider.Ollama);
        viewModel.SelectedProviderOption = ollamaOption;
        viewModel.OllamaEndpoint = string.Empty;
        viewModel.SelectedModel = "llama3";

        Assert.False(viewModel.SaveCommand.CanExecute(null));

        viewModel.OllamaEndpoint = "http://localhost:11434/";
        Assert.True(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task SaveCommand_UpdatesProviderSelection()
    {
        var settings = new AppSettings
        {
            Provider = LlmProvider.OpenAi,
            OpenAi = new ProviderSettings { ApiKey = "openai", Model = "gpt-4o-mini" },
            Anthropic = new ProviderSettings { ApiKey = "anthropic", Model = "claude-3-haiku" }
        };

        var settingsService = new RecordingSettingsService();
        var modelCatalogService = new StubModelCatalogService(new[] { "claude-3-haiku", "claude-3-sonnet" });
        var sessionService = new StubSessionPersistenceService();
        var viewModel = new SettingsViewModel(settingsService, modelCatalogService, settings, sessionService);

        var anthropicOption = viewModel.Providers.First(p => p.Provider == LlmProvider.Anthropic);
        viewModel.SelectedProviderOption = anthropicOption;
        viewModel.AnthropicApiKey = "anthropic-updated";
        viewModel.SelectedModel = "claude-3-sonnet";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(settingsService.LastSaved);
        Assert.Equal(LlmProvider.Anthropic, settingsService.LastSaved.Provider);
        Assert.Equal("anthropic-updated", settingsService.LastSaved.Anthropic.ApiKey);
        Assert.Equal("claude-3-sonnet", settingsService.LastSaved.Anthropic.Model);
    }

    [Fact]
    public async Task SaveCommand_PreservesProjectsCollection()
    {
        var project = new ProjectSettings { Id = Guid.NewGuid().ToString("N"), Name = "Project Alpha" };
        var settings = new AppSettings
        {
            Provider = LlmProvider.OpenAi,
            OpenAi = new ProviderSettings { ApiKey = "openai", Model = "gpt-4o-mini" },
            Projects = new List<ProjectSettings> { project },
            ActiveProjectId = project.Id
        };

        var settingsService = new RecordingSettingsService();
        var modelCatalogService = new StubModelCatalogService();
        var sessionService = new StubSessionPersistenceService();
        var viewModel = new SettingsViewModel(settingsService, modelCatalogService, settings, sessionService);

        viewModel.OpenAiApiKey = "openai-updated";
        viewModel.SelectedModel = "gpt-4o-mini";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(settingsService.LastSaved);
        Assert.Single(settingsService.LastSaved!.Projects);
        Assert.Equal(project.Id, settingsService.LastSaved.Projects[0].Id);
        Assert.Equal("Project Alpha", settingsService.LastSaved.Projects[0].Name);
        Assert.Equal(project.Id, settingsService.LastSaved.ActiveProjectId);
    }

    [Fact]
    public async Task FetchModelsCommand_LoadsModels()
    {
        var settings = new AppSettings
        {
            Provider = LlmProvider.OpenAi,
            OpenAi = new ProviderSettings { ApiKey = "openai-key", Model = "gpt-4o-mini" }
        };

        var settingsService = new RecordingSettingsService();
        var modelCatalogService = new StubModelCatalogService(new[] { "gpt-4o-mini", "gpt-4o" });
        var sessionService = new StubSessionPersistenceService();
        var viewModel = new SettingsViewModel(settingsService, modelCatalogService, settings, sessionService);

        Assert.True(viewModel.FetchModelsCommand.CanExecute(null));

        await viewModel.FetchModelsCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.AvailableModels.Count);
        Assert.Contains("gpt-4o-mini", viewModel.AvailableModels);
        Assert.Contains("gpt-4o", viewModel.AvailableModels);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.SelectedModel));
    }

    [Fact]
    public async Task PurgeChatsCommand_InvokesService()
    {
        var settings = new AppSettings
        {
            Provider = LlmProvider.OpenAi,
            OpenAi = new ProviderSettings { ApiKey = "openai-key", Model = "gpt-4o-mini" }
        };

        var settingsService = new RecordingSettingsService();
        var modelCatalogService = new StubModelCatalogService();
        var sessionService = new StubSessionPersistenceService();
        var viewModel = new SettingsViewModel(settingsService, modelCatalogService, settings, sessionService);

        await viewModel.PurgeChatsCommand.ExecuteAsync(null);

        Assert.Equal(1, sessionService.PurgeCallCount);
        Assert.Contains("purged", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingSettingsService : ISettingsService
    {
        public AppSettings? LastSaved { get; private set; }

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new AppSettings());

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            LastSaved = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class StubModelCatalogService : IModelCatalogService
    {
        private readonly IReadOnlyList<string> _models;

        public StubModelCatalogService()
            : this(new List<string>())
        {
        }

        public StubModelCatalogService(IReadOnlyList<string> models)
        {
            _models = models;
        }

        public Task<IReadOnlyList<string>> GetModelsAsync(LlmProvider provider, ProviderSettings providerSettings, CancellationToken cancellationToken)
            => Task.FromResult(_models);
    }

    private sealed class StubSessionPersistenceService : ISessionPersistenceService
    {
        public int PurgeCallCount { get; private set; }

        public Task<SessionStoreSnapshot> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new SessionStoreSnapshot());

        public Task SaveAsync(SessionStoreSnapshot snapshot, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task PurgeAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PurgeCallCount++;
            return Task.CompletedTask;
        }
    }
}
