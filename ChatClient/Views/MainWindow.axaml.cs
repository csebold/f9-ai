using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ChatClient.Models;
using ChatClient.Services;
using ChatClient.ViewModels;

namespace ChatClient.Views;

public partial class MainWindow : Window
{
    private const string DefaultProjectName = "Default Project";

    private readonly ISettingsService _settingsService;
    private readonly IModelCatalogService _modelCatalogService;
    private readonly MenuItem _projectsMenuRoot;

    private AppSettings _settings;
    private ProjectSettings? _activeProject;

    public MainWindow()
        : this(new SettingsService(), new ModelCatalogService(), new AppSettings(), activeProject: null)
    {
    }

    public MainWindow(ISettingsService settingsService, IModelCatalogService modelCatalogService, AppSettings settings, ProjectSettings? activeProject)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _modelCatalogService = modelCatalogService ?? throw new ArgumentNullException(nameof(modelCatalogService));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _activeProject = activeProject;

        InitializeComponent();

        _projectsMenuRoot = this.FindControl<MenuItem>("ProjectsMenuRoot")
                            ?? throw new InvalidOperationException("Projects menu root not found.");

        Opened += OnOpened;
    }

    public async Task InitializeAsync()
    {
        RefreshProjectsMenu();
        await ApplyProjectAsync(_activeProject, persist: false, isUpdate: false);
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        await InitializeAsync();
    }

    private async void SettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as MainWindowViewModel;
        if (viewModel is null)
        {
            return;
        }

        var previousActiveId = _activeProject?.Id;

        var dialog = new SettingsWindow
        {
            DataContext = new SettingsViewModel(_settingsService, _modelCatalogService, _settings)
        };

        var result = await ShowSettingsDialogAsync(dialog);
        if (result is null)
        {
            return;
        }

        _settings = result;
        _activeProject = string.IsNullOrWhiteSpace(previousActiveId)
            ? null
            : _settings.Projects.FirstOrDefault(p => string.Equals(p.Id, previousActiveId, StringComparison.Ordinal));

        RefreshProjectsMenu();
        await ApplyProjectAsync(_activeProject, persist: false, isUpdate: true);
    }

    private async void ProjectSettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_activeProject is null)
        {
            return;
        }

        var dialog = new ProjectEditorWindow
        {
            DataContext = new ProjectEditorViewModel(_activeProject, _settings, isNewProject: false)
        };

        var updated = await ShowProjectEditorAsync(dialog);
        if (updated is null)
        {
            return;
        }

        var target = _settings.Projects.FirstOrDefault(p => string.Equals(p.Id, updated.Id, StringComparison.Ordinal));
        if (target is null)
        {
            return;
        }

        CopyProjectSettings(target, updated);
        ProjectWorkspace.EnsureWorkspace(target);

        _activeProject = target;
        await ApplyProjectAsync(_activeProject, persist: true, isUpdate: true);
    }

    private async void ProjectMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
        {
            return;
        }

        if (menuItem.CommandParameter is ProjectSettings project)
        {
            if (_activeProject?.Id == project.Id)
            {
                return;
            }

            await ApplyProjectAsync(project, persist: true, isUpdate: true);
        }
        else
        {
            if (_activeProject is null)
            {
                return;
            }

            await ApplyProjectAsync(project: null, persist: true, isUpdate: true);
        }
    }

    private async void AddProjectMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        var newProject = new ProjectSettings();
        var dialog = new ProjectEditorWindow
        {
            DataContext = new ProjectEditorViewModel(newProject, _settings, isNewProject: true)
        };

        var result = await ShowProjectEditorAsync(dialog);
        if (result is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(result.Id))
        {
            result.Id = Guid.NewGuid().ToString("N");
        }

        ProjectWorkspace.EnsureWorkspace(result);

        _settings.Projects.Add(result);
        _activeProject = result;

        RefreshProjectsMenu();
        await ApplyProjectAsync(_activeProject, persist: true, isUpdate: true);
    }

    private async Task<AppSettings?> ShowSettingsDialogAsync(SettingsWindow dialog)
    {
        try
        {
            return await dialog.ShowDialog<AppSettings?>(this);
        }
        catch
        {
            return null;
        }
    }

    private async Task<ProjectSettings?> ShowProjectEditorAsync(ProjectEditorWindow dialog)
    {
        try
        {
            return await dialog.ShowDialog<ProjectSettings?>(this);
        }
        catch
        {
            return null;
        }
    }

    private async Task ApplyProjectAsync(ProjectSettings? project, bool persist, bool isUpdate)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (project is not null)
        {
            ProjectWorkspace.EnsureWorkspace(project);
        }

        LlmClientRegistration registration;
        try
        {
            registration = LlmClientFactory.CreateForProject(_settings, project);
        }
        catch (Exception ex)
        {
            var detail = $"LLM configuration error: {ex.Message}";
            registration = new LlmClientRegistration(new FallbackLlmClient(detail), "Unavailable", "N/A", detail);
        }

        _activeProject = project;
        _settings.ActiveProjectId = _activeProject?.Id;

        var projectName = project?.Name ?? DefaultProjectName;
        var instructions = project?.Instructions ?? string.Empty;
        var hasCustomProject = project is not null;

        viewModel.ChangeProject(registration, projectName, instructions, hasCustomProject, isUpdate);

        RefreshProjectsMenu();

        if (persist)
        {
            try
            {
                await _settingsService.SaveAsync(_settings);
            }
            catch
            {
                // Intentionally ignored. User will attempt again if needed.
            }
        }
    }

    private void RefreshProjectsMenu()
    {
        var items = new AvaloniaList<object>
        {
            CreateProjectMenuItem(project: null, isActive: _activeProject is null)
        };

        items.Add(new Separator());

        var orderedProjects = _settings.Projects
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (orderedProjects.Count > 0)
        {
            foreach (var project in orderedProjects)
            {
                var isActive = _activeProject is not null && string.Equals(_activeProject.Id, project.Id, StringComparison.Ordinal);
                items.Add(CreateProjectMenuItem(project, isActive));
            }

            items.Add(new Separator());
        }

        items.Add(CreateAddProjectMenuItem());

        _projectsMenuRoot.ItemsSource = items;
    }

    private MenuItem CreateProjectMenuItem(ProjectSettings? project, bool isActive)
    {
        var header = project?.Name ?? DefaultProjectName;
        var item = new MenuItem
        {
            Header = header,
            IsChecked = isActive,
            ToggleType = MenuItemToggleType.CheckBox,
            CommandParameter = project
        };

        item.Click += ProjectMenuItem_OnClick;
        return item;
    }

    private MenuItem CreateAddProjectMenuItem()
    {
        var item = new MenuItem
        {
            Header = "Add Project..."
        };
        item.Click += AddProjectMenuItem_OnClick;
        return item;
    }

    private static void CopyProjectSettings(ProjectSettings target, ProjectSettings source)
    {
        target.Name = source.Name;
        target.Instructions = source.Instructions;
        target.Provider = source.Provider;
        target.ApiKey = source.ApiKey;
        target.Model = source.Model;
        target.WorkspacePath = source.WorkspacePath;
        target.ThemeId = source.ThemeId;
        target.FontFamily = source.FontFamily;
    }
}
