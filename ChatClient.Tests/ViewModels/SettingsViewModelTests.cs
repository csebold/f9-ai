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
        var viewModel = new SettingsViewModel(settingsService, modelCatalogService, settings);

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
    public async Task FetchModelsCommand_LoadsModels()
    {
        var settings = new AppSettings
        {
            Provider = LlmProvider.OpenAi,
            OpenAi = new ProviderSettings { ApiKey = "openai-key", Model = "gpt-4o-mini" }
        };

        var settingsService = new RecordingSettingsService();
        var modelCatalogService = new StubModelCatalogService(new[] { "gpt-4o-mini", "gpt-4o" });
        var viewModel = new SettingsViewModel(settingsService, modelCatalogService, settings);

        Assert.True(viewModel.FetchModelsCommand.CanExecute(null));

        await viewModel.FetchModelsCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.AvailableModels.Count);
        Assert.Contains("gpt-4o-mini", viewModel.AvailableModels);
        Assert.Contains("gpt-4o", viewModel.AvailableModels);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.SelectedModel));
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

        public Task<IReadOnlyList<string>> GetModelsAsync(LlmProvider provider, string apiKey, CancellationToken cancellationToken)
            => Task.FromResult(_models);
    }
}
