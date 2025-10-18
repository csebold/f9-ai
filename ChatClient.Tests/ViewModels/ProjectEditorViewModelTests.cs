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

public class ProjectEditorViewModelTests
{
    [Fact]
    public void SaveCommand_EmitsTrimmedProjectWithOverrides()
    {
        var project = new ProjectSettings();
        var defaults = new AppSettings
        {
            Provider = LlmProvider.OpenAi,
            OpenAi = new ProviderSettings { ApiKey = "openai-default", Model = "gpt-4o-mini" }
        };

        var viewModel = new ProjectEditorViewModel(project, defaults, isNewProject: true, new StubModelCatalogService());
        ProjectSettings? saved = null;
        viewModel.Saved += (_, result) => saved = result;

        viewModel.Name = "  Example Project  ";
        viewModel.Description = "  quick summary  ";
        viewModel.Instructions = "  keep it short  ";
        viewModel.ApiKey = "  key-123  ";
        viewModel.Model = "  custom-model  ";
        viewModel.Endpoint = "  http://localhost:9900/llm  ";

        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(saved);
        Assert.Equal("Example Project", saved!.Name);
        Assert.Equal("quick summary", saved.Description);
        Assert.Equal("keep it short", saved.Instructions);
        Assert.Equal("key-123", saved.ApiKey);
        Assert.Equal("custom-model", saved.Model);
        Assert.Equal("http://localhost:9900/llm", saved.Endpoint);
        Assert.False(string.IsNullOrWhiteSpace(saved.WorkspacePath));
        Assert.Null(saved.Provider);
    }

    [Fact]
    public void SaveCommand_RespectsNameValidation()
    {
        var project = new ProjectSettings();
        var defaults = new AppSettings();

        var viewModel = new ProjectEditorViewModel(project, defaults, isNewProject: true, new StubModelCatalogService());

        viewModel.Name = "   ";
        Assert.False(viewModel.SaveCommand.CanExecute(null));

        viewModel.Name = "Valid Name";
        Assert.True(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void Constructor_SelectsExistingProvider()
    {
        var project = new ProjectSettings
        {
            Name = "Existing Project",
            Provider = LlmProvider.OpenRouter
        };

        var defaults = new AppSettings
        {
            Provider = LlmProvider.OpenAi,
            OpenRouter = new ProviderSettings { ApiKey = "router", Model = "openrouter/auto" }
        };

        var viewModel = new ProjectEditorViewModel(project, defaults, isNewProject: false, new StubModelCatalogService());

        Assert.Equal(LlmProvider.OpenRouter, viewModel.SelectedProviderOption.Provider);
        Assert.Equal("Existing Project", viewModel.Name);
    }

    [Fact]
    public void SaveCommand_PersistsSelectedProvider()
    {
        var project = new ProjectSettings();
        var defaults = new AppSettings
        {
            Provider = LlmProvider.OpenAi
        };

        var viewModel = new ProjectEditorViewModel(project, defaults, isNewProject: true, new StubModelCatalogService());
        viewModel.Name = "Ollama Project";
        viewModel.SelectedProviderOption = viewModel.Providers.First(p => p.Provider == LlmProvider.Ollama);

        ProjectSettings? saved = null;
        viewModel.Saved += (_, result) => saved = result;

        viewModel.SaveCommand.Execute(null);

        Assert.NotNull(saved);
        Assert.Equal(LlmProvider.Ollama, saved!.Provider);
    }

    [Fact]
    public void ChangingProvider_ClearsModelOverride()
    {
        var project = new ProjectSettings
        {
            Model = "gpt-4o-mini"
        };

        var defaults = new AppSettings
        {
            Provider = LlmProvider.OpenAi
        };

        var viewModel = new ProjectEditorViewModel(project, defaults, isNewProject: false, new StubModelCatalogService());

        Assert.Equal("gpt-4o-mini", viewModel.Model);
        Assert.NotNull(viewModel.SelectedModelOption);

        viewModel.SelectedProviderOption = viewModel.Providers.First(p => p.Provider == LlmProvider.Ollama);

        Assert.Equal(string.Empty, viewModel.Model);
        Assert.Null(viewModel.SelectedModelOption?.ModelId);
    }

    private sealed class StubModelCatalogService : IModelCatalogService
    {
        public Task<IReadOnlyList<ModelCatalogEntry>> GetModelsAsync(LlmProvider provider, ProviderSettings providerSettings, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ModelCatalogEntry>>(Array.Empty<ModelCatalogEntry>());

        public Task DownloadModelAsync(LlmProvider provider, ProviderSettings providerSettings, string modelId, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
