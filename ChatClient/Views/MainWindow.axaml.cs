using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ChatClient.Models;
using ChatClient.Services;
using ChatClient.ViewModels;
using ChatClient.Utilities;

namespace ChatClient.Views;

public partial class MainWindow : Window
{
    private const string DefaultProjectName = "Default Project";
    private const string DefaultProjectKey = "default";
    private const double AutoScrollThreshold = 32;
    private const int MaxChatFileInlineBytes = 128 * 1024;

    private static readonly IReadOnlyDictionary<string, string> KnownMimeTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".txt"] = "text/plain",
        [".md"] = "text/markdown",
        [".markdown"] = "text/markdown",
        [".json"] = "application/json",
        [".jsonl"] = "application/json",
        [".yaml"] = "application/yaml",
        [".yml"] = "application/yaml",
        [".xml"] = "application/xml",
        [".csv"] = "text/csv",
        [".tsv"] = "text/tab-separated-values",
        [".log"] = "text/plain",
        [".html"] = "text/html",
        [".htm"] = "text/html",
        [".css"] = "text/css",
        [".js"] = "application/javascript",
        [".ts"] = "text/plain",
        [".py"] = "text/x-python",
        [".cs"] = "text/plain",
        [".java"] = "text/plain",
        [".go"] = "text/plain",
        [".rb"] = "text/plain",
        [".rs"] = "text/plain",
        [".c"] = "text/plain",
        [".cpp"] = "text/plain",
        [".h"] = "text/plain",
        [".hpp"] = "text/plain",
        [".swift"] = "text/plain",
        [".kt"] = "text/plain",
        [".sql"] = "text/plain",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".bmp"] = "image/bmp",
        [".webp"] = "image/webp",
        [".pdf"] = "application/pdf"
    };

    private readonly ISettingsService _settingsService;
    private readonly IModelCatalogService _modelCatalogService;
    private readonly ISessionPersistenceService _sessionPersistenceService;
    private readonly IBackgroundProcessService _backgroundProcessService;
    private readonly IOllamaProcessManager _ollamaProcessManager;
    private readonly IProviderBrandingService _providerBrandingService;
    private readonly IProjectFileService _projectFileService;
    private readonly IChatFileService _chatFileService;
    private readonly ChatLogService _chatLogService = new();
    private readonly ObservableCollection<ProjectListItem> _projectItems = new();
    private readonly ObservableCollection<ChatSessionListItem> _sessionItems = new();
    private readonly Dictionary<string, ProjectSessionState> _sessionStates = new(StringComparer.Ordinal);
    private readonly ListBox _projectsList;
    private readonly ListBox _sessionsList;
    private readonly SemaphoreSlim _sessionSaveLock = new(1, 1);
    private Grid? _conversationLayout;
    private ScrollViewer? _conversationScrollViewer;
    private TextBox? _composerTextBox;
    private bool _shouldAutoScroll = true;
    private bool _pendingAutoScroll;

    private AppSettings _settings;
    private ProjectSettings? _activeProject;
    private ChatSessionState? _activeSession;
    private MainWindowViewModel? _viewModel;
    private SessionStoreSnapshot _sessionSnapshot;
    private bool _suppressProjectSelectionChanged;
    private bool _suppressSessionSelectionChanged;
    private bool _suppressMessageSync;
    private readonly bool _ownsBackgroundService;
    private readonly bool _ownsOllamaManager;
    private readonly bool _ownsProviderBrandingService;
    private readonly bool _ownsProjectFileService;

    public MainWindow()
        : this(
            new SettingsService(),
            new ModelCatalogService(),
            new SessionPersistenceService(),
            new AppSettings(),
            activeProject: null,
            sessionSnapshot: new SessionStoreSnapshot(),
            projectFileService: new ProjectFileService(),
            chatFileService: new ChatFileService())
    {
    }

    public MainWindow(
        ISettingsService settingsService,
        IModelCatalogService modelCatalogService,
        ISessionPersistenceService sessionPersistenceService,
        AppSettings settings,
        ProjectSettings? activeProject,
        SessionStoreSnapshot? sessionSnapshot,
        IBackgroundProcessService? backgroundProcessService = null,
        IOllamaProcessManager? ollamaProcessManager = null,
        IProviderBrandingService? providerBrandingService = null,
        IProjectFileService? projectFileService = null,
        IChatFileService? chatFileService = null)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _modelCatalogService = modelCatalogService ?? throw new ArgumentNullException(nameof(modelCatalogService));
        _sessionPersistenceService = sessionPersistenceService ?? throw new ArgumentNullException(nameof(sessionPersistenceService));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _activeProject = activeProject;
        _sessionSnapshot = sessionSnapshot ?? new SessionStoreSnapshot();
        _backgroundProcessService = backgroundProcessService ?? new BackgroundProcessService();
        _ollamaProcessManager = ollamaProcessManager ?? new OllamaProcessManager(_backgroundProcessService);
        _providerBrandingService = providerBrandingService ?? new ProviderBrandingService();
        _projectFileService = projectFileService ?? new ProjectFileService();
        _chatFileService = chatFileService ?? new ChatFileService();
        _ownsBackgroundService = backgroundProcessService is null;
        _ownsOllamaManager = ollamaProcessManager is null;
        _ownsProviderBrandingService = providerBrandingService is null;
        _ownsProjectFileService = projectFileService is null;
        _backgroundProcessService.ProcessChanged += OnBackgroundProcessChanged;
        ApplyOllamaRuntimeDefaults();

        InitializeComponent();

        _projectsList = this.FindControl<ListBox>("ProjectsList")
                        ?? throw new InvalidOperationException("Projects list control not found.");
        _sessionsList = this.FindControl<ListBox>("SessionsList")
                        ?? throw new InvalidOperationException("Sessions list control not found.");
        _conversationLayout = this.FindControl<Grid>("ConversationLayout");
        _conversationScrollViewer = this.FindControl<ScrollViewer>("ConversationScrollViewer");
        _composerTextBox = this.FindControl<TextBox>("ComposerTextBox")
                           ?? throw new InvalidOperationException("Composer text box not found.");

        _projectsList.ItemsSource = _projectItems;
        _sessionsList.ItemsSource = _sessionItems;
        if (_conversationScrollViewer is not null)
        {
            _conversationScrollViewer.ScrollChanged += OnConversationScrollChanged;
        }

        _composerTextBox.AddHandler(InputElement.KeyDownEvent, ComposerTextBox_OnKeyDown, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);

        ScalingChanged += OnScalingChanged;

        DataContextChanged += OnDataContextChanged;
        Opened += OnOpened;
    }

    public async Task InitializeAsync()
    {
        BuildSessionStates();
        RefreshProjectList();
        await ApplyProjectAsync(_activeProject, persist: false, isUpdate: false, emitStatusMessageOverride: false);
    }

    private void ApplyOllamaRuntimeDefaults()
    {
        if (_settings.OllamaRuntime is null)
        {
            _settings.OllamaRuntime = new OllamaRuntimeSettings();
        }

        _ollamaProcessManager.UpdateEnvironmentDefaults(_settings.OllamaRuntime);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Messages.CollectionChanged -= OnMessagesCollectionChanged;
            _viewModel.SetChatLogHandle(null);
        }

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.Messages.CollectionChanged += OnMessagesCollectionChanged;
            ResetAutoScroll(requestScroll: true);
            _viewModel.InitializeBackgroundProcesses(_backgroundProcessService, _backgroundProcessService.GetProcesses());
        }
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        await InitializeAsync();
        ApplyDensityScaling();
    }

    private void OnScalingChanged(object? sender, EventArgs e) => ApplyDensityScaling();

    private void ApplyDensityScaling()
    {
        if (_conversationLayout is null)
        {
            return;
        }

        var classes = _conversationLayout.Classes;
        classes.Remove("density-compact");
        classes.Remove("density-comfortable");

        var scaling = RenderScaling;

        if (scaling >= 1.5)
        {
            classes.Add("density-comfortable");
        }
        else if (scaling <= 1.0)
        {
            classes.Add("density-compact");
        }
    }

    private void ComposerTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        if (_viewModel is null || sender is not TextBox textBox)
        {
            return;
        }

        if (TryHandleMacEditingShortcut(textBox, e))
        {
            e.Handled = true;
            return;
        }

        if (!IsEnterKey(e.Key))
        {
            return;
        }

        if (!TryHandleSendKey(textBox, e))
        {
            return;
        }

        e.Handled = true;
    }

    private bool TryHandleSendKey(TextBox textBox, KeyEventArgs e)
    {
        var activation = _settings.ChatInput?.SendActivation ?? ChatSendActivation.Enter;
        var modifiers = NormalizeModifiers(e.KeyModifiers);

        if (activation == ChatSendActivation.Enter)
        {
            if (modifiers == KeyModifiers.None && ShouldSendOnPlainEnter(textBox))
            {
                return TryExecuteSendCommand();
            }

            if (OperatingSystem.IsMacOS())
            {
                if (MatchesExplicitActivation(modifiers, ChatSendActivation.CommandEnter))
                {
                    return TryExecuteSendCommand();
                }
            }
            else
            {
                if (MatchesExplicitActivation(modifiers, ChatSendActivation.ControlEnter))
                {
                    return TryExecuteSendCommand();
                }
            }

            return false;
        }

        if (!MatchesExplicitActivation(modifiers, activation))
        {
            return false;
        }

        return TryExecuteSendCommand();
    }

    private bool TryExecuteSendCommand()
    {
        if (_viewModel is null)
        {
            return false;
        }

        if (!_viewModel.SendCommand.CanExecute(null))
        {
            return false;
        }

        _ = _viewModel.SendCommand.ExecuteAsync(null);
        return true;
    }

    private static bool ShouldSendOnPlainEnter(TextBox textBox)
    {
        var text = textBox.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0)
        {
            return false;
        }

        return !LooksLikeSingleLineMarkdown(text);
    }

    private static bool LooksLikeSingleLineMarkdown(string text)
    {
        var trimmed = text.TrimStart();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var first = trimmed[0];
        if (first == '#' || first == '*' || first == '-')
        {
            return true;
        }

        var index = 0;
        while (index < trimmed.Length && char.IsDigit(trimmed[index]))
        {
            index++;
        }

        if (index > 0 && index < trimmed.Length && (trimmed[index] == '.' || trimmed[index] == ')'))
        {
            return true;
        }

        return false;
    }

    private static bool MatchesExplicitActivation(KeyModifiers modifiers, ChatSendActivation activation)
    {
        return activation switch
        {
            ChatSendActivation.ShiftEnter => modifiers.HasFlag(KeyModifiers.Shift),
            ChatSendActivation.ControlEnter => modifiers.HasFlag(KeyModifiers.Control),
            ChatSendActivation.CommandEnter => OperatingSystem.IsMacOS()
                ? modifiers.HasFlag(KeyModifiers.Meta)
                : modifiers.HasFlag(KeyModifiers.Control),
            ChatSendActivation.Enter => modifiers == KeyModifiers.None,
            _ => false
        };
    }

    private static bool IsEnterKey(Key key) =>
        key == Key.Enter || key == Key.Return;

    private static KeyModifiers NormalizeModifiers(KeyModifiers modifiers) => modifiers;

    private static bool TryHandleMacEditingShortcut(TextBox textBox, KeyEventArgs e)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        var modifiers = NormalizeModifiers(e.KeyModifiers);
        if (modifiers != KeyModifiers.Control)
        {
            return false;
        }

        var text = textBox.Text ?? string.Empty;
        var caret = Math.Clamp(textBox.CaretIndex, 0, text.Length);
        var lineStart = GetLineStart(text, caret);
        var lineEnd = GetLineEnd(text, caret);

        switch (e.Key)
        {
            case Key.A:
                MoveCaret(textBox, lineStart);
                return true;
            case Key.E:
                MoveCaret(textBox, lineEnd);
                return true;
            case Key.K:
                if (lineEnd > caret)
                {
                    textBox.SelectionStart = caret;
                    textBox.SelectionEnd = lineEnd;
                    textBox.SelectedText = string.Empty;
                }
                else
                {
                    var newlineLength = GetNewLineLength(text, lineEnd);
                    if (newlineLength > 0)
                    {
                        textBox.SelectionStart = caret;
                        textBox.SelectionEnd = Math.Min(text.Length, lineEnd + newlineLength);
                        textBox.SelectedText = string.Empty;
                    }
                }

                return true;
            case Key.F:
                if (caret < text.Length)
                {
                    MoveCaret(textBox, caret + 1);
                }

                return true;
            case Key.B:
                if (caret > 0)
                {
                    MoveCaret(textBox, caret - 1);
                }

                return true;
            case Key.N:
            {
                var nextLineStart = GetNextLineStart(text, lineEnd);
                if (nextLineStart > text.Length - 1)
                {
                    return true;
                }

                var nextLineEnd = GetLineEnd(text, nextLineStart);
                var column = caret - lineStart;
                var target = nextLineStart + Math.Min(column, nextLineEnd - nextLineStart);
                MoveCaret(textBox, target);
                return true;
            }
            case Key.P:
            {
                var previousLineStart = GetPreviousLineStart(text, lineStart);
                if (previousLineStart < 0)
                {
                    return true;
                }

                var previousLineEnd = GetLineEnd(text, previousLineStart);
                var column = caret - lineStart;
                var target = previousLineStart + Math.Min(column, previousLineEnd - previousLineStart);
                MoveCaret(textBox, target);
                return true;
            }
            case Key.D:
                if (caret < text.Length)
                {
                    textBox.SelectionStart = caret;
                    textBox.SelectionEnd = Math.Min(text.Length, caret + 1);
                    textBox.SelectedText = string.Empty;
                }

                return true;
            case Key.H:
                if (caret > 0)
                {
                    textBox.SelectionStart = caret - 1;
                    textBox.SelectionEnd = caret;
                    textBox.SelectedText = string.Empty;
                }

                return true;
            default:
                return false;
        }
    }

    private static void MoveCaret(TextBox textBox, int index)
    {
        var bounded = Math.Clamp(index, 0, textBox.Text?.Length ?? 0);
        textBox.SelectionStart = bounded;
        textBox.SelectionEnd = bounded;
        textBox.CaretIndex = bounded;
    }

    private static int GetLineStart(string text, int index)
    {
        var position = Math.Clamp(index, 0, text.Length);
        while (position > 0)
        {
            var ch = text[position - 1];
            if (ch == '\n' || ch == '\r')
            {
                break;
            }

            position--;
        }

        return position;
    }

    private static int GetLineEnd(string text, int index)
    {
        var position = Math.Clamp(index, 0, text.Length);
        while (position < text.Length)
        {
            var ch = text[position];
            if (ch == '\n' || ch == '\r')
            {
                break;
            }

            position++;
        }

        return position;
    }

    private static int GetNextLineStart(string text, int lineEnd)
    {
        var position = Math.Clamp(lineEnd, 0, text.Length);
        while (position < text.Length && (text[position] == '\n' || text[position] == '\r'))
        {
            position++;
        }

        return position;
    }

    private static int GetPreviousLineStart(string text, int currentLineStart)
    {
        var position = Math.Clamp(currentLineStart, 0, text.Length);
        if (position == 0)
        {
            return -1;
        }

        position--;

        while (position >= 0 && (text[position] == '\n' || text[position] == '\r'))
        {
            position--;
        }

        if (position < 0)
        {
            return 0;
        }

        return GetLineStart(text, position + 1);
    }

    private static int GetNewLineLength(string text, int lineEnd)
    {
        if (lineEnd >= text.Length)
        {
            return 0;
        }

        if (text[lineEnd] == '\r')
        {
            var length = 1;
            if (lineEnd + 1 < text.Length && text[lineEnd + 1] == '\n')
            {
                length++;
            }

            return length;
        }

        return text[lineEnd] == '\n' ? 1 : 0;
    }

    private async void SettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var previousActiveId = _activeProject?.Id;
        var previousProvider = _settings.Provider;
        var previousOllamaModel = (_settings.Ollama.Model ?? string.Empty).Trim();
        var previousOllamaEndpoint = NormalizeEndpoint(_settings.Ollama.Endpoint);

        var dialog = new SettingsWindow
        {
            DataContext = new SettingsViewModel(_settingsService, _modelCatalogService, _settings, _sessionPersistenceService)
        };

        var result = await ShowSettingsDialogAsync(dialog);
        if (result is null)
        {
            return;
        }

        _settings = result;
        ApplyOllamaRuntimeDefaults();
        var newProvider = _settings.Provider;
        var newOllamaModel = (_settings.Ollama.Model ?? string.Empty).Trim();
        var newOllamaEndpoint = NormalizeEndpoint(_settings.Ollama.Endpoint);

        var defaultOllamaChanged = newProvider == LlmProvider.Ollama &&
                                   (previousProvider != LlmProvider.Ollama ||
                                    !string.Equals(previousOllamaModel, newOllamaModel, StringComparison.Ordinal) ||
                                    !string.Equals(previousOllamaEndpoint, newOllamaEndpoint, StringComparison.OrdinalIgnoreCase));

        if (defaultOllamaChanged)
        {
            await EnsureOllamaServerForEndpointAsync(_settings.Ollama.Endpoint, "for default settings");
        }

        _activeProject = string.IsNullOrWhiteSpace(previousActiveId)
            ? null
            : _settings.Projects.FirstOrDefault(p => string.Equals(p.Id, previousActiveId, StringComparison.Ordinal));

        RefreshProjectList();
        await ApplyProjectAsync(_activeProject, persist: false, isUpdate: true, emitStatusMessageOverride: true);
    }

    private async void ProjectSettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_activeProject is null)
        {
            return;
        }

        var previousProvider = _activeProject.Provider;
        var previousModel = (_activeProject.Model ?? string.Empty).Trim();
        var previousEndpoint = NormalizeEndpoint(_activeProject.Endpoint);

        var dialog = new ProjectEditorWindow
        {
            DataContext = new ProjectEditorViewModel(_activeProject, _settings, isNewProject: false, _modelCatalogService)
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

        var projectModelChanged = target.Provider == LlmProvider.Ollama &&
                                  (previousProvider != LlmProvider.Ollama ||
                                   !string.Equals(previousModel, (target.Model ?? string.Empty).Trim(), StringComparison.Ordinal) ||
                                   !string.Equals(previousEndpoint, NormalizeEndpoint(target.Endpoint), StringComparison.OrdinalIgnoreCase));

        if (projectModelChanged)
        {
            var projectName = string.IsNullOrWhiteSpace(target.Name) ? "project" : $"project '{target.Name}'";
            await EnsureOllamaServerForEndpointAsync(target.Endpoint, $"for {projectName}");
        }

        _activeProject = target;
        RefreshProjectList();
        await ApplyProjectAsync(_activeProject, persist: true, isUpdate: true, emitStatusMessageOverride: true);
    }

    private async void AddProjectButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var newProject = new ProjectSettings();
        var dialog = new ProjectEditorWindow
        {
            DataContext = new ProjectEditorViewModel(newProject, _settings, isNewProject: true, _modelCatalogService)
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

        RefreshProjectList();
        await ApplyProjectAsync(_activeProject, persist: true, isUpdate: true, emitStatusMessageOverride: true);
    }

    private async void ProjectsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressProjectSelectionChanged)
        {
            return;
        }

        if (_projectsList.SelectedItem is not ProjectListItem selectedItem)
        {
            return;
        }

        if (_activeProject is null && selectedItem.Project is null)
        {
            return;
        }

        if (_activeProject?.Id is not null &&
            selectedItem.Project?.Id is not null &&
            string.Equals(_activeProject.Id, selectedItem.Project.Id, StringComparison.Ordinal))
        {
            return;
        }

        await ApplyProjectAsync(selectedItem.Project, persist: true, isUpdate: true, emitStatusMessageOverride: true);
    }

    private async void SessionsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSessionSelectionChanged)
        {
            return;
        }

        if (_sessionsList.SelectedItem is not ChatSessionListItem item)
        {
            return;
        }

        if (_activeSession?.Id == item.Session.Id)
        {
            return;
        }

        var projectKey = GetProjectKey(_activeProject);
        await ApplySessionAsync(projectKey, item.Session);
        RefreshSessionsForProject(projectKey, item.Session.Id);
    }

    private async void NewChatButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var projectKey = GetProjectKey(_activeProject);
        var projectState = EnsureProjectSession(projectKey);
        var session = CreateNewSession(projectState);

        projectState.ActiveSessionId = session.Id;
        _settings.ActiveSessions[projectKey] = session.Id;

        await ApplySessionAsync(projectKey, session);
        RefreshSessionsForProject(projectKey, session.Id);
    }

    private void OpenChatLogButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.ChatLogPath is null)
        {
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _viewModel.ChatLogPath,
                UseShellExecute = true
            };
            Process.Start(startInfo);
            _viewModel.StatusMessage = $"Opened chat log at {_viewModel.ChatLogPath}";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"Failed to open chat log: {ex.Message}";
            _viewModel.ErrorSummary = ex.Message;
        }
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

    private async Task ApplyProjectAsync(ProjectSettings? project, bool persist, bool isUpdate, bool? emitStatusMessageOverride = null)
    {
        if (_viewModel is null)
        {
            return;
        }

        LlmClientRegistration registration;
        try
        {
            registration = LlmClientFactory.CreateForProject(_settings, project);
        }
        catch (Exception ex)
        {
            var detail = $"LLM configuration error: {ex.Message}";
            var provider = project?.Provider ?? _settings.Provider;
            registration = new LlmClientRegistration(new FallbackLlmClient(detail), provider, "Unavailable", "N/A", detail);
        }

        var projectFilesSummary = string.Empty;
        IReadOnlyList<ProjectFile> projectFiles = Array.Empty<ProjectFile>();
        if (project is not null)
        {
            ProjectWorkspace.EnsureWorkspace(project);
            var filesDirectory = ProjectWorkspace.GetProjectFilesDirectory(project);
            projectFiles = _projectFileService.GetFiles(project);
            projectFilesSummary = ProjectFileSummaryBuilder.Build(filesDirectory);
        }

        string? ollamaWarning = null;
        string? ollamaError = null;
        var attemptedOllamaStartup = false;
        if (registration.Provider == LlmProvider.Ollama)
        {
            var selectionResult = await EnsureOllamaModelSelectionAsync(registration, project, CancellationToken.None);
            if (selectionResult.ErrorMessage is not null && selectionResult.IsConnectionFailure)
            {
                attemptedOllamaStartup = true;
                await EnsureOllamaRunningAsync(registration, project, CancellationToken.None);
                selectionResult = await EnsureOllamaModelSelectionAsync(registration, project, CancellationToken.None);
            }

            if (selectionResult.ErrorMessage is not null)
            {
                if (selectionResult.IsConnectionFailure && attemptedOllamaStartup)
                {
                    ollamaError = $"{selectionResult.ErrorMessage} Attempted to launch Ollama automatically, but it still did not respond.";
                }
                else
                {
                    ollamaError = selectionResult.ErrorMessage;
                }

                registration = CreateOllamaFallbackRegistration(registration, ollamaError);
            }
            else
            {
                registration = selectionResult.Registration;
                ollamaWarning = selectionResult.WarningMessage;
            }
        }

        ProviderBranding? providerBranding = null;
        try
        {
            providerBranding = await _providerBrandingService.GetBrandingAsync(registration.Provider, CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            providerBranding = null;
        }

        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (_viewModel is null)
            {
                return;
            }

            _activeProject = project;
            var projectKey = GetProjectKey(project);
            _settings.ActiveProjectId = project?.Id;

            var projectState = EnsureProjectSession(projectKey);

            _settings.ActiveSessions.TryGetValue(projectKey, out var persistedSessionId);
            var activeSessionId = ResolveActiveSessionId(projectState, persistedSessionId);
            var activeSession = projectState.Sessions.First(s => string.Equals(s.Id, activeSessionId, StringComparison.Ordinal));

            projectState.ActiveSessionId = activeSession.Id;
            _settings.ActiveSessions[projectKey] = activeSession.Id;

            var emitStatusMessage = emitStatusMessageOverride ?? !activeSession.Messages.Any();
            var shouldPersistSessions = persist || isUpdate || emitStatusMessage;

            _activeSession = activeSession;
            ActivateChatLog(activeSession);
            _suppressMessageSync = true;
            _viewModel.ResetMessages(activeSession.Messages, includeWelcomeWhenEmpty: true);
            ResetAutoScroll(requestScroll: true);
            SyncSessionWithViewModel();
            _suppressMessageSync = false;

            var projectName = project?.Name ?? DefaultProjectName;
            var instructions = project?.Instructions ?? string.Empty;
            var description = project?.Description ?? string.Empty;
            var hasCustomProject = project is not null;

            _viewModel.ReplaceProjectFiles(projectFiles, projectFilesSummary);
            _viewModel.ChangeProject(registration, projectName, instructions, description, hasCustomProject, projectFilesSummary, isUpdate, emitStatusMessage);
            RefreshChatFiles(_activeProject, _activeSession);
            _viewModel.ApplyProviderBranding(providerBranding);

            if (ollamaError is not null)
            {
                _viewModel.StatusMessage = ollamaError;
                _viewModel.ErrorSummary = ollamaError;
            }

            if (registration.Provider == LlmProvider.Ollama)
            {
                await EnsureOllamaRunningAsync(registration, project, CancellationToken.None);
            }

            RefreshProjectList();
            RefreshSessionsForProject(projectKey, activeSession.Id);

            if (shouldPersistSessions)
            {
                await PersistSessionsAsync();
            }
            if (persist)
            {
                try
                {
                    await _settingsService.SaveAsync(_settings);
                }
                catch
                {
                    // Ignore persistence failures; user can retry later.
                }
            }
        });

        if (ollamaError is not null)
        {
            await PostSystemNoticeAsync(ollamaError, isError: true);
        }
        else if (!string.IsNullOrWhiteSpace(ollamaWarning))
        {
            await PostSystemNoticeAsync(ollamaWarning, isError: false);
        }
    }

    private async void AddProjectFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var project = _activeProject;
        if (project is null)
        {
            _viewModel.StatusMessage = "Project files are available after saving a project.";
            return;
        }

        if (StorageProvider is null)
        {
            _viewModel.StatusMessage = "File picker is not available on this platform.";
            return;
        }

        var options = new FilePickerOpenOptions
        {
            AllowMultiple = true,
            Title = "Add project files"
        };

        var files = await StorageProvider.OpenFilePickerAsync(options);
        if (files is null || files.Count == 0)
        {
            return;
        }

        var addedCount = 0;
        foreach (var file in files)
        {
            try
            {
                await AddProjectFileAsync(project, file);
                addedCount++;
            }
            catch (Exception ex)
            {
                await PostSystemNoticeAsync($"Failed to add '{file.Name}': {ex.Message}", isError: true);
            }
        }

        RefreshProjectFiles(project);

        if (addedCount > 0)
        {
            _viewModel.StatusMessage = addedCount == 1
                ? "Added 1 file to the project."
                : $"Added {addedCount} files to the project.";
        }
    }

    private async void DeleteProjectFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (_activeProject is null)
        {
            return;
        }

        if (sender is not Button button || button.Tag is not ProjectFileItemViewModel item)
        {
            return;
        }

        try
        {
            await _projectFileService.DeleteFileAsync(_activeProject, item.Name, CancellationToken.None).ConfigureAwait(false);
            RefreshProjectFiles(_activeProject);
            _viewModel.StatusMessage = $"Removed '{item.Name}' from the project.";
        }
        catch (Exception ex)
        {
            await PostSystemNoticeAsync($"Failed to remove '{item.Name}': {ex.Message}", isError: true);
        }
    }

    private async void AddChatFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (_activeProject is null || _activeSession is null)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storageProvider)
        {
            return;
        }

        var options = new FilePickerOpenOptions
        {
            Title = "Add chat files",
            AllowMultiple = true
        };

        var files = await storageProvider.OpenFilePickerAsync(options);
        if (files is null || files.Count == 0)
        {
            return;
        }

        var uploadedFiles = new List<ProjectFile>();
        var addedCount = 0;

        foreach (var file in files)
        {
            try
            {
                var uploaded = await AddChatFileAsync(_activeProject, _activeSession, file);
                uploadedFiles.Add(uploaded);
                addedCount++;
            }
            catch (Exception ex)
            {
                await PostSystemNoticeAsync($"Failed to add chat file '{file.Name}': {ex.Message}", isError: true);
            }
        }

        RefreshChatFiles(_activeProject, _activeSession);

        if (addedCount > 0)
        {
            _viewModel.StatusMessage = addedCount == 1
                ? "Added 1 chat file to the conversation."
                : $"Added {addedCount} chat files to the conversation.";
        }

        if (uploadedFiles.Count > 0)
        {
            await RegisterChatFileUploadsAsync(uploadedFiles);
        }
    }

    private async void DeleteChatFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (_activeProject is null || _activeSession is null)
        {
            return;
        }

        if (sender is not Button button || button.Tag is not ProjectFileItemViewModel item)
        {
            return;
        }

        try
        {
            await _chatFileService.DeleteFileAsync(_activeProject, _activeSession.Id, item.Name, CancellationToken.None);
            RefreshChatFiles(_activeProject, _activeSession);
            _viewModel.StatusMessage = $"Removed chat file '{item.Name}'.";
        }
        catch (Exception ex)
        {
            await PostSystemNoticeAsync($"Failed to remove chat file '{item.Name}': {ex.Message}", isError: true);
        }
    }

    private async Task AddProjectFileAsync(ProjectSettings project, IStorageFile file)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        if (file is null)
        {
            throw new ArgumentNullException(nameof(file));
        }

        await AddStorageFileAsync(
            file,
            path => _projectFileService.AddFileAsync(project, path, CancellationToken.None));
    }

    private async Task<ProjectFile> AddChatFileAsync(ProjectSettings project, ChatSessionState session, IStorageFile file)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        if (session is null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        return await AddStorageFileAsync(
            file,
            path => _chatFileService.AddFileAsync(project, session.Id, path, CancellationToken.None));
    }

    private static async Task<ProjectFile> AddStorageFileAsync(IStorageFile file, Func<string, Task<ProjectFile>> addOperation)
    {
        if (file is null)
        {
            throw new ArgumentNullException(nameof(file));
        }

        if (addOperation is null)
        {
            throw new ArgumentNullException(nameof(addOperation));
        }

        var localPath = file.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(localPath) && File.Exists(localPath))
        {
            return await addOperation(localPath);
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), "f9-upload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        var sanitizedName = SanitizeFileName(file.Name);
        if (string.IsNullOrWhiteSpace(sanitizedName))
        {
            sanitizedName = "uploaded-file";
        }

        var tempPath = Path.Combine(tempDirectory, sanitizedName);

        await using var sourceStream = await file.OpenReadAsync();
        await using (var tempStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await sourceStream.CopyToAsync(tempStream).ConfigureAwait(false);
        }

        try
        {
            return await addOperation(tempPath);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
            catch
            {
                // best effort cleanup
            }
        }
    }

    private static string SanitizeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var invalid = Path.GetInvalidFileNameChars();
        var buffer = name.ToCharArray();

        for (var i = 0; i < buffer.Length; i++)
        {
            if (invalid.Contains(buffer[i]))
            {
                buffer[i] = '_';
            }
        }

        return new string(buffer).Trim();
    }

    private void RefreshProjectFiles(ProjectSettings? project)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (project is null)
        {
            _viewModel.ReplaceProjectFiles(Array.Empty<ProjectFile>(), string.Empty);
            return;
        }

        var filesDirectory = ProjectWorkspace.GetProjectFilesDirectory(project);
        var files = _projectFileService.GetFiles(project);
        var summary = ProjectFileSummaryBuilder.Build(filesDirectory);

        _viewModel.ReplaceProjectFiles(files, summary);
    }

    private void RefreshChatFiles(ProjectSettings? project, ChatSessionState? session)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (project is null || session is null)
        {
            _viewModel.ReplaceChatFiles(Array.Empty<ProjectFile>(), string.Empty);
            return;
        }

        var filesDirectory = ProjectWorkspace.GetChatFilesDirectory(project, session.Id);
        var files = _chatFileService.GetFiles(project, session.Id);
        var summary = ProjectFileSummaryBuilder.Build(filesDirectory, scopeName: "Chat");

        _viewModel.ReplaceChatFiles(files, summary);
    }

    private async Task RegisterChatFileUploadsAsync(IEnumerable<ProjectFile> files)
    {
        if (_viewModel is null)
        {
            return;
        }

        foreach (var file in files)
        {
            var (mimeType, preview, isBinary, isTruncated) = await ReadChatFilePreviewAsync(file);
            await _viewModel.RegisterChatFileAttachmentAsync(file, mimeType, preview, isTruncated, isBinary);
        }
    }

    private static async Task<(string MimeType, string? Preview, bool IsBinary, bool IsTruncated)> ReadChatFilePreviewAsync(ProjectFile file)
    {
        var mimeType = GetMimeType(file.Name);
        var (preview, truncated, detectedBinary) = await TryReadTextPreviewAsync(file.FullPath, file.Length);

        if (detectedBinary)
        {
            return (mimeType, null, true, false);
        }

        if (preview is not null)
        {
            if (!IsTextMimeType(mimeType))
            {
                mimeType = "text/plain";
            }

            return (mimeType, preview, false, truncated);
        }

        return (mimeType, null, true, false);
    }

    private static string GetMimeType(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "application/octet-stream";
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            return "application/octet-stream";
        }

        if (KnownMimeTypes.TryGetValue(extension, out var mime))
        {
            return mime;
        }

        return "application/octet-stream";
    }

    private static bool IsTextMimeType(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            return false;
        }

        if (mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return mimeType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
               || mimeType.Equals("application/xml", StringComparison.OrdinalIgnoreCase)
               || mimeType.Equals("application/yaml", StringComparison.OrdinalIgnoreCase)
               || mimeType.Equals("application/javascript", StringComparison.OrdinalIgnoreCase)
               || mimeType.Equals("text/markdown", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(string? Preview, bool IsTruncated, bool IsBinary)> TryReadTextPreviewAsync(string path, long fileLength)
    {
        if (!File.Exists(path))
        {
            return (null, false, true);
        }

        var bytesToRead = (int)Math.Min(MaxChatFileInlineBytes, Math.Max(0, fileLength));
        if (bytesToRead == 0)
        {
            return (string.Empty, false, false);
        }

        var buffer = new byte[bytesToRead];
        var read = 0;

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        read = await stream.ReadAsync(buffer, 0, bytesToRead);

        if (read == 0)
        {
            return (string.Empty, false, false);
        }

        if (ContainsBinaryData(buffer.AsSpan(0, read)))
        {
            return (null, false, true);
        }

        try
        {
            var text = Encoding.UTF8.GetString(buffer, 0, read);
            var truncated = fileLength > read;
            return (text, truncated, false);
        }
        catch (DecoderFallbackException)
        {
            return (null, false, true);
        }
    }

    private static bool ContainsBinaryData(ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
        {
            if (b == 0)
            {
                return true;
            }

            if (b < 0x09)
            {
                return true;
            }

            if (b > 0x0D && b < 0x20)
            {
                return true;
            }
        }

        return false;
    }

    private static LlmClientRegistration CreateOllamaFallbackRegistration(LlmClientRegistration original, string message)
    {
        var fallback = new LlmClientRegistration(
            new FallbackLlmClient(message),
            original.Provider,
            original.ProviderDisplayName,
            "Unavailable",
            message)
        {
            Endpoint = original.Endpoint
        };

        return fallback;
    }

    private readonly record struct OllamaSelectionResult(
        LlmClientRegistration Registration,
        string? WarningMessage,
        string? ErrorMessage,
        bool IsConnectionFailure);

    private async Task<OllamaSelectionResult> EnsureOllamaModelSelectionAsync(LlmClientRegistration registration, ProjectSettings? project, CancellationToken cancellationToken)
    {
        var endpoint = ResolveOllamaEndpoint(registration, project) ?? string.Empty;
        var providerSettings = new ProviderSettings
        {
            Endpoint = endpoint,
            Model = registration.ModelId
        };

        IReadOnlyList<ModelCatalogEntry> models;
        try
        {
            models = await _modelCatalogService.GetModelsAsync(LlmProvider.Ollama, providerSettings, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (!cancellationToken.IsCancellationRequested)
        {
            var endpointDisplay = string.IsNullOrWhiteSpace(endpoint)
                ? "http://localhost:11434/"
                : endpoint;
            var detail = BuildOllamaConnectionHint(ex);
            var message = $"Could not reach Ollama at {endpointDisplay}. {detail} Start the Ollama app or run 'ollama serve', then try again.";
            return new OllamaSelectionResult(registration, null, message, true);
        }
        catch (Exception ex)
        {
            return new OllamaSelectionResult(registration, null, $"Failed to inspect Ollama models: {ex.Message}", false);
        }

        var installed = models
            .Where(m => m.IsInstalled)
            .ToList();

        if (installed.Any(m => string.Equals(m.Id, registration.ModelId, StringComparison.OrdinalIgnoreCase)))
        {
            return new OllamaSelectionResult(registration, null, null, false);
        }

        if (installed.Count == 0)
        {
            var endpointDisplay = string.IsNullOrWhiteSpace(endpoint) ? "the configured endpoint" : endpoint;
            var message = $"Model '{registration.ModelId}' is not installed and no Ollama models are available at {endpointDisplay}. Download a model with 'ollama pull <model>' or install one from Settings > Models before continuing.";
            return new OllamaSelectionResult(registration, null, message, false);
        }

        var fallbackModel = installed[0].Id;
        providerSettings.Model = fallbackModel;

        var fallbackRegistration = LlmClientFactory.CreateOllamaClient(providerSettings);
        var warning = $"Model '{registration.ModelId}' is not installed. Using '{fallbackModel}' instead.";
        return new OllamaSelectionResult(fallbackRegistration, warning, null, false);
    }

    private static string BuildOllamaConnectionHint(HttpRequestException exception)
    {
        var detail = exception.InnerException switch
        {
            SocketException { SocketErrorCode: SocketError.ConnectionRefused } => "The connection was refused, which usually means the Ollama service is not running.",
            SocketException { SocketErrorCode: SocketError.HostNotFound } => "The host name could not be resolved.",
            SocketException { SocketErrorCode: SocketError.TimedOut } => "The request to Ollama timed out before it responded.",
            _ => null
        };

        if (string.IsNullOrWhiteSpace(detail))
        {
            detail = string.IsNullOrWhiteSpace(exception.Message)
                ? "The Ollama endpoint did not respond."
                : exception.Message.Trim();
        }

        if (!detail.EndsWith(".", StringComparison.Ordinal))
        {
            detail += ".";
        }

        return detail;
    }

    private async Task PostSystemNoticeAsync(string message, bool isError)
    {
        if (_viewModel is null)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _viewModel.Messages.Add(new MessageViewModel(new Message("System", message, DateTimeOffset.Now, MessageRole.System)));
            if (isError)
            {
                _viewModel.ErrorSummary = message;
            }

            _viewModel.StatusMessage = message;
        });
    }

    private async Task EnsureOllamaRunningAsync(LlmClientRegistration registration, ProjectSettings? project, CancellationToken cancellationToken)
    {
        var endpoint = ResolveOllamaEndpoint(registration, project);

        if (_viewModel is not null)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _viewModel.StatusMessage = "Ensuring Ollama server is running...";
                _viewModel.ErrorSummary = null;
            });
        }

        try
        {
            OllamaProcessEnsureResult ensureResult;
            try
            {
                ensureResult = await _ollamaProcessManager.EnsureServerAsync(endpoint, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Failed to start process 'ollama'", StringComparison.OrdinalIgnoreCase))
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                ensureResult = await _ollamaProcessManager.EnsureServerAsync(endpoint, cancellationToken).ConfigureAwait(false);
            }

            if (_viewModel is not null && ensureResult.AlreadyRunningExternally && ensureResult.ManagedProcess is not null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _viewModel.ApplyBackgroundProcessChange(ensureResult.ManagedProcess, BackgroundProcessChangeKind.Added);
                });
            }

            if (_viewModel is not null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (ensureResult.ManagedProcess is { IsHealthy: false } snapshot)
                    {
                        var detail = snapshot.StatusMessage ?? "Ollama did not report ready before the timeout.";
                        if (!string.IsNullOrWhiteSpace(snapshot.LogPath))
                        {
                            detail += $" See {snapshot.LogPath} for details.";
                        }

                        var message = $"Ollama server is running but not ready: {detail}";
                        _viewModel.StatusMessage = message;
                        _viewModel.ErrorSummary = message;
                    }
                    else
                    {
                        _viewModel.StatusMessage = $"Ready - {registration.ProviderDisplayName} ({registration.ModelId})";
                    }
                });
            }
        }
        catch (Exception ex)
        {
            if (_viewModel is not null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var message = $"Ollama startup failed: {ex.Message}";
                    if (!message.EndsWith(".", StringComparison.Ordinal))
                    {
                        message += ".";
                    }

                    message += " Start the Ollama app and ensure at least one model is installed (for example, run 'ollama pull llama3').";
                    _viewModel.ErrorSummary = message;
                    _viewModel.StatusMessage = message;
                });
            }
        }
    }

    private async Task EnsureOllamaServerForEndpointAsync(string? endpoint, string contextDescription)
    {
        try
        {
            var normalizedEndpoint = string.IsNullOrWhiteSpace(endpoint) ? null : endpoint.Trim();
            var ensureResult = await _ollamaProcessManager.EnsureServerAsync(normalizedEndpoint, CancellationToken.None).ConfigureAwait(false);

            if (_viewModel is not null && ensureResult.AlreadyRunningExternally && ensureResult.ManagedProcess is not null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _viewModel.ApplyBackgroundProcessChange(ensureResult.ManagedProcess, BackgroundProcessChangeKind.Added);
                });
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await PostSystemNoticeAsync($"Failed to start Ollama {contextDescription}: {ex.Message}", isError: true);
        }
    }

    private static string? ResolveOllamaEndpoint(LlmClientRegistration registration, ProjectSettings? project)
    {
        if (registration.Endpoint is not null)
        {
            return registration.Endpoint.ToString();
        }

        if (!string.IsNullOrWhiteSpace(project?.Endpoint))
        {
            return project.Endpoint;
        }

        return null;
    }

    private static string NormalizeEndpoint(string? endpoint)
    {
        return string.IsNullOrWhiteSpace(endpoint)
            ? string.Empty
            : endpoint.Trim().TrimEnd('/');
    }

    private void OnBackgroundProcessChanged(object? sender, BackgroundProcessChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_viewModel is null)
            {
                return;
            }

            _viewModel.ApplyBackgroundProcessChange(e.Snapshot, e.Kind);
        });
    }

    private Task ApplySessionAsync(string projectKey, ChatSessionState session)
    {
        if (_viewModel is null)
        {
            return Task.CompletedTask;
        }

        _activeSession = session;
        ActivateChatLog(session);

        _suppressMessageSync = true;
        _viewModel.ResetMessages(session.Messages, includeWelcomeWhenEmpty: true);
        ResetAutoScroll(requestScroll: true);
        SyncSessionWithViewModel();
        _suppressMessageSync = false;

        RefreshChatFiles(_activeProject, _activeSession);

        var state = EnsureProjectSession(projectKey);
        state.ActiveSessionId = session.Id;
        _settings.ActiveSessions[projectKey] = session.Id;

        return PersistSessionsAsync();
    }

    private void BuildSessionStates()
    {
        _sessionStates.Clear();

        foreach (var projectSnapshot in _sessionSnapshot.Projects)
        {
            var projectKey = string.IsNullOrWhiteSpace(projectSnapshot.ProjectId)
                ? DefaultProjectKey
                : projectSnapshot.ProjectId;

            var state = EnsureProjectSession(projectKey);
            state.Sessions.Clear();

            foreach (var sessionSnapshot in projectSnapshot.Sessions)
            {
                var session = new ChatSessionState(
                    sessionSnapshot.Id,
                    sessionSnapshot.Title,
                    sessionSnapshot.CreatedAt,
                    sessionSnapshot.UpdatedAt,
                    sessionSnapshot.Messages,
                    sessionSnapshot.LogPath);
                EnsureSessionLogPath(session);
                UpdateSessionMetadata(session);
                state.Sessions.Add(session);
            }

            if (state.Sessions.Count == 0)
            {
                CreateNewSession(state);
            }

            state.ActiveSessionId = ResolveActiveSessionId(state, projectSnapshot.ActiveSessionId);
            _sessionStates[projectKey] = state;
        }
    }

    private void RefreshProjectList()
    {
        var selectedKey = GetProjectKey(_activeProject);

        _suppressProjectSelectionChanged = true;
        try
        {
            _projectsList.SelectedIndex = -1;

            _projectItems.Clear();
            var defaultItem = new ProjectListItem(DefaultProjectKey, null, DefaultProjectName);
            _projectItems.Add(defaultItem);

            var orderedProjects = _settings.Projects
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var project in orderedProjects)
            {
                var key = GetProjectKey(project);
                var name = string.IsNullOrWhiteSpace(project.Name) ? DefaultProjectName : project.Name;
                _projectItems.Add(new ProjectListItem(key, project, name));
            }

            var selected = _projectItems.FirstOrDefault(p => string.Equals(p.ProjectKey, selectedKey, StringComparison.Ordinal))
                           ?? defaultItem;
            _projectsList.SelectedItem = selected;
        }
        finally
        {
            _suppressProjectSelectionChanged = false;
        }
    }

    private void RefreshSessionsForProject(string projectKey, string? activeSessionId)
    {
        var state = EnsureProjectSession(projectKey);

        _suppressSessionSelectionChanged = true;
        try
        {
            foreach (var session in state.Sessions)
            {
                UpdateSessionMetadata(session);
            }

            var orderedSessions = state.Sessions
                .OrderByDescending(s => s.UpdatedAt)
                .ToList();

            _sessionsList.SelectedIndex = -1;
            _sessionItems.Clear();

            foreach (var session in orderedSessions)
            {
                _sessionItems.Add(new ChatSessionListItem(session));
            }

            if (orderedSessions.Count == 0)
            {
                return;
            }

            var targetId = activeSessionId
                           ?? state.ActiveSessionId
                           ?? orderedSessions.FirstOrDefault()?.Id;

            if (!string.IsNullOrWhiteSpace(targetId))
            {
                state.ActiveSessionId = targetId;
                _settings.ActiveSessions[projectKey] = targetId;
            }

            var selected = _sessionItems.FirstOrDefault(i => string.Equals(i.Session.Id, targetId, StringComparison.Ordinal))
                          ?? _sessionItems.FirstOrDefault();
            _sessionsList.SelectedItem = selected;
        }
        finally
        {
            _suppressSessionSelectionChanged = false;
        }
    }

    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_suppressMessageSync)
        {
            return;
        }

        if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Reset)
        {
            var forceScroll = e.Action == NotifyCollectionChangedAction.Reset;
            QueueScrollToBottom(forceScroll);
        }

        SyncSessionWithViewModel();

        if (_activeSession is not null)
        {
            var projectKey = GetProjectKey(_activeProject);
            RefreshSessionsForProject(projectKey, _activeSession.Id);
        }

        _ = PersistSessionsAsync();
    }

    private void QueueScrollToBottom(bool force)
    {
        if (_conversationScrollViewer is null)
        {
            return;
        }

        if (!force && !_shouldAutoScroll)
        {
            _pendingAutoScroll = true;
            return;
        }

        _pendingAutoScroll = false;

        Dispatcher.UIThread.Post(() =>
        {
            if (_conversationScrollViewer is null)
            {
                return;
            }

            if (!force && !_shouldAutoScroll)
            {
                _pendingAutoScroll = true;
                return;
            }

            var extentHeight = _conversationScrollViewer.Extent.Height;
            var viewportHeight = _conversationScrollViewer.Viewport.Height;

            if (double.IsNaN(extentHeight) || double.IsNaN(viewportHeight))
            {
                _pendingAutoScroll = true;
                return;
            }

            var targetOffset = Math.Max(0, extentHeight - viewportHeight);
            if (double.IsInfinity(targetOffset))
            {
                return;
            }

            var offset = _conversationScrollViewer.Offset;
            if (Math.Abs(offset.Y - targetOffset) > 0.5)
            {
                _conversationScrollViewer.Offset = new Vector(offset.X, targetOffset);
            }
        }, DispatcherPriority.Background);
    }

    private void ResetAutoScroll(bool requestScroll)
    {
        _shouldAutoScroll = true;
        _pendingAutoScroll = false;

        if (requestScroll)
        {
            QueueScrollToBottom(force: true);
        }
    }

    private void OnConversationScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_conversationScrollViewer is null)
        {
            return;
        }

        if (IsNearBottom(_conversationScrollViewer))
        {
            _shouldAutoScroll = true;

            if (_pendingAutoScroll)
            {
                QueueScrollToBottom(force: false);
            }
        }
        else
        {
            _shouldAutoScroll = false;
        }
    }

    private static bool IsNearBottom(ScrollViewer scroller)
    {
        var extentHeight = scroller.Extent.Height;
        var viewportHeight = scroller.Viewport.Height;

        if (double.IsNaN(extentHeight) || double.IsNaN(viewportHeight))
        {
            return true;
        }

        if (extentHeight <= viewportHeight)
        {
            return true;
        }

        var maxOffset = Math.Max(0, extentHeight - viewportHeight);
        var delta = maxOffset - scroller.Offset.Y;
        return delta <= AutoScrollThreshold;
    }

    private void SyncSessionWithViewModel()
    {
        if (_activeSession is null || _viewModel is null)
        {
            return;
        }

        _activeSession.Messages.Clear();
        foreach (var messageViewModel in _viewModel.Messages)
        {
            _activeSession.Messages.Add(messageViewModel.UnderlyingMessage);
        }

        UpdateSessionMetadata(_activeSession);

        var projectKey = GetProjectKey(_activeProject);
        var state = EnsureProjectSession(projectKey);
        if (!state.Sessions.Contains(_activeSession))
        {
            state.Sessions.Add(_activeSession);
        }

        state.ActiveSessionId = _activeSession.Id;
        _settings.ActiveSessions[projectKey] = _activeSession.Id;
    }

    private async Task PersistSessionsAsync()
    {
        if (!_settings.EnableSessionPersistence)
        {
            return;
        }

        await _sessionSaveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var snapshot = BuildSnapshot();
            SessionStatePruner.Trim(snapshot, _settings.MaxSessionsPerProject, _settings.MaxMessagesPerSession);
            AlignStateWithSnapshot(snapshot);
            _sessionSnapshot = snapshot;
            await _sessionPersistenceService.SaveAsync(snapshot).ConfigureAwait(false);
        }
        catch
        {
            // Persistence failures are ignored; the user can retry the operation later.
        }
        finally
        {
            _sessionSaveLock.Release();
        }
    }

    private SessionStoreSnapshot BuildSnapshot()
    {
        var snapshot = new SessionStoreSnapshot
        {
            Version = SessionStoreSnapshot.CurrentVersion
        };

        foreach (var (projectKey, state) in _sessionStates)
        {
            var projectSnapshot = new ProjectSessionSnapshot
            {
                ProjectId = projectKey,
                ActiveSessionId = state.ActiveSessionId
            };

            foreach (var session in state.Sessions)
            {
                projectSnapshot.Sessions.Add(new ChatSessionSnapshot
                {
                    Id = session.Id,
                    Title = session.Title,
                    CreatedAt = session.CreatedAt,
                    UpdatedAt = session.UpdatedAt,
                    Messages = session.Messages.ToList(),
                    LogPath = session.LogPath
                });
            }

            snapshot.Projects.Add(projectSnapshot);
        }

        return snapshot;
    }

    private void AlignStateWithSnapshot(SessionStoreSnapshot snapshot)
    {
        foreach (var project in snapshot.Projects)
        {
            var projectKey = string.IsNullOrWhiteSpace(project.ProjectId)
                ? DefaultProjectKey
                : project.ProjectId;

            var state = EnsureProjectSession(projectKey);
            var existing = state.Sessions.ToDictionary(s => s.Id, s => s, StringComparer.Ordinal);

            state.Sessions.Clear();

            foreach (var sessionSnapshot in project.Sessions)
            {
                if (existing.TryGetValue(sessionSnapshot.Id, out var sessionState))
                {
                    sessionState.Title = sessionSnapshot.Title;
                    sessionState.CreatedAt = sessionSnapshot.CreatedAt;
                    sessionState.UpdatedAt = sessionSnapshot.UpdatedAt;
                    sessionState.LogPath = sessionSnapshot.LogPath;
                    sessionState.Messages.Clear();
                    sessionState.Messages.AddRange(sessionSnapshot.Messages);
                }
                else
                {
                    sessionState = new ChatSessionState(
                        sessionSnapshot.Id,
                        sessionSnapshot.Title,
                        sessionSnapshot.CreatedAt,
                        sessionSnapshot.UpdatedAt,
                        sessionSnapshot.Messages,
                        sessionSnapshot.LogPath);
                }

                EnsureSessionLogPath(sessionState);
                state.Sessions.Add(sessionState);
            }

            state.ActiveSessionId = project.ActiveSessionId ?? state.Sessions.FirstOrDefault()?.Id;

            if (!string.IsNullOrWhiteSpace(state.ActiveSessionId))
            {
                _settings.ActiveSessions[projectKey] = state.ActiveSessionId;
            }
            else
            {
                _settings.ActiveSessions.Remove(projectKey);
            }

            if (IsProjectKeyActive(projectKey))
            {
                _activeSession = state.Sessions.FirstOrDefault(s => string.Equals(s.Id, state.ActiveSessionId, StringComparison.Ordinal))
                                 ?? state.Sessions.FirstOrDefault();
            }
        }
    }

    private ChatLogHandle EnsureSessionLogPath(ChatSessionState session)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        var handle = _chatLogService.GetOrCreate(session.Id, session.LogPath);
        session.LogPath = handle.Path;
        return handle;
    }

    private void ActivateChatLog(ChatSessionState session)
    {
        var handle = EnsureSessionLogPath(session);
        _viewModel?.SetChatLogHandle(handle);
    }

    private ProjectSessionState EnsureProjectSession(string projectKey)
    {
        if (!_sessionStates.TryGetValue(projectKey, out var state))
        {
            state = new ProjectSessionState(projectKey);
            _sessionStates[projectKey] = state;
        }

        if (state.Sessions.Count == 0)
        {
            CreateNewSession(state);
        }

        return state;
    }

    private ChatSessionState CreateNewSession(ProjectSessionState projectState)
    {
        var now = DateTimeOffset.UtcNow;
        var session = new ChatSessionState(Guid.NewGuid().ToString("N"), "New Chat", now, now);
        projectState.Sessions.Add(session);
        EnsureSessionLogPath(session);
        return session;
    }

    private static void CopyProjectSettings(ProjectSettings target, ProjectSettings source)
    {
        target.Name = source.Name;
        target.Description = source.Description;
        target.Instructions = source.Instructions;
        target.Provider = source.Provider;
        target.ApiKey = source.ApiKey;
        target.Model = source.Model;
        target.Endpoint = source.Endpoint;
        target.WorkspacePath = source.WorkspacePath;
        target.ThemeId = source.ThemeId;
        target.FontFamily = source.FontFamily;
    }

    private bool IsProjectKeyActive(string projectKey)
    {
        if (_activeProject is null)
        {
            return string.Equals(projectKey, DefaultProjectKey, StringComparison.Ordinal);
        }

        return string.Equals(projectKey, _activeProject.Id, StringComparison.Ordinal);
    }

    private static string GetProjectKey(ProjectSettings? project)
    {
        if (project is null)
        {
            return DefaultProjectKey;
        }

        if (string.IsNullOrWhiteSpace(project.Id))
        {
            project.Id = Guid.NewGuid().ToString("N");
        }

        return project.Id;
    }

    private static void UpdateSessionMetadata(ChatSessionState session)
    {
        var lastMessage = session.Messages.LastOrDefault();
        session.UpdatedAt = lastMessage?.Timestamp ?? DateTimeOffset.UtcNow;

        var userMessage = session.Messages.FirstOrDefault(m => m.Role == MessageRole.User && !string.IsNullOrWhiteSpace(m.Content));
        if (userMessage is not null)
        {
            session.Title = TrimTitle(userMessage.Content);
        }
        else if (session.Messages.Count == 0)
        {
            session.Title = "New Chat";
        }
    }

    private static string TrimTitle(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "New Chat";
        }

        var normalized = content.Replace("\r", " ")
                                .Replace("\n", " ")
                                .Trim();

        const int MaxLength = 60;
        if (normalized.Length > MaxLength)
        {
            normalized = normalized.Substring(0, MaxLength).TrimEnd() + "...";
        }

        return string.IsNullOrWhiteSpace(normalized) ? "New Chat" : normalized;
    }

    private static string ResolveActiveSessionId(ProjectSessionState state, string? preferredSessionId = null)
    {
        if (!string.IsNullOrWhiteSpace(preferredSessionId) &&
            state.Sessions.Any(s => string.Equals(s.Id, preferredSessionId, StringComparison.Ordinal)))
        {
            return preferredSessionId;
        }

        if (!string.IsNullOrWhiteSpace(state.ActiveSessionId) &&
            state.Sessions.Any(s => string.Equals(s.Id, state.ActiveSessionId, StringComparison.Ordinal)))
        {
            return state.ActiveSessionId;
        }

        return state.Sessions
            .OrderByDescending(s => s.UpdatedAt)
            .First()
            .Id;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        _backgroundProcessService.ProcessChanged -= OnBackgroundProcessChanged;

        if (_ownsOllamaManager)
        {
            _ollamaProcessManager.Dispose();
        }

        if (_ownsBackgroundService)
        {
            _backgroundProcessService.Dispose();
        }

        if (_ownsProviderBrandingService && _providerBrandingService is IDisposable brandingDisposable)
        {
            brandingDisposable.Dispose();
        }

        if (_viewModel is not null)
        {
            _viewModel.Messages.CollectionChanged -= OnMessagesCollectionChanged;
            _viewModel.SetChatLogHandle(null);
        }

        if (_conversationScrollViewer is not null)
        {
            _conversationScrollViewer.ScrollChanged -= OnConversationScrollChanged;
        }

        ScalingChanged -= OnScalingChanged;
        _chatLogService.Dispose();
    }

    public sealed class ProjectListItem
    {
        public ProjectListItem(string projectKey, ProjectSettings? project, string displayName)
        {
            ProjectKey = projectKey;
            Project = project;
            DisplayName = displayName;
        }

        public string ProjectKey { get; }

        public ProjectSettings? Project { get; }

        public string DisplayName { get; }
    }

    private sealed class ProjectSessionState
    {
        public ProjectSessionState(string projectKey)
        {
            ProjectKey = projectKey;
        }

        public string ProjectKey { get; }

        public List<ChatSessionState> Sessions { get; } = new();

        public string? ActiveSessionId { get; set; }
    }

    public sealed class ChatSessionState
    {
        public ChatSessionState(string id, string title, DateTimeOffset createdAt, DateTimeOffset updatedAt, IEnumerable<Message>? messages = null, string? logPath = null)
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            Title = string.IsNullOrWhiteSpace(title) ? "New Chat" : title;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
            Messages = messages?.Where(m => m is not null).ToList() ?? new List<Message>();
            LogPath = string.IsNullOrWhiteSpace(logPath) ? null : logPath;
        }

        public string Id { get; }

        public string Title { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public List<Message> Messages { get; }

        public string? LogPath { get; set; }
    }

    public sealed class ChatSessionListItem
    {
        public ChatSessionListItem(ChatSessionState session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Title = string.IsNullOrWhiteSpace(session.Title) ? "New Chat" : session.Title;
            Subtitle = TimestampFormatter.GetSessionSubtitle(session.UpdatedAt);
        }

        public ChatSessionState Session { get; }

        public string Title { get; }

        public string Subtitle { get; }
    }
}
