using System;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Models;

namespace ChatClient.Services;

public sealed class StartupInitializer : IStartupInitializer
{
    private readonly ISettingsService _settingsService;
    private readonly IProjectWorkspaceService _workspaceService;
    private readonly IProviderBrandingService _brandingService;

    public StartupInitializer(ISettingsService settingsService, IProjectWorkspaceService workspaceService, IProviderBrandingService brandingService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _workspaceService = workspaceService ?? throw new ArgumentNullException(nameof(workspaceService));
        _brandingService = brandingService ?? throw new ArgumentNullException(nameof(brandingService));
    }

    public async Task<StartupInitializationResult> InitializeAsync(
        IProgress<string> statusProgress,
        CancellationToken cancellationToken)
    {
        var reporter = statusProgress ?? new Progress<string>(_ => { });

        reporter.Report("Loading user settings...");
        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);

        var settingsUpdated = false;

        foreach (var project in settings.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var projectLabel = string.IsNullOrWhiteSpace(project.Name)
                ? project.Id
                : project.Name;

            reporter.Report($"Preparing workspace for '{projectLabel}'...");
            if (_workspaceService.EnsureWorkspace(project))
            {
                settingsUpdated = true;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        reporter.Report("Selecting active project...");
        var activeProject = ResolveActiveProject(settings, ref settingsUpdated);

        var providerForStatus = activeProject?.Provider ?? settings.Provider;
        var providerDisplayName = providerForStatus.ToString();

        cancellationToken.ThrowIfCancellationRequested();

        reporter.Report("Refreshing provider branding...");
        try
        {
            await _brandingService.RefreshAllAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            reporter.Report($"Failed to refresh provider branding: {ex.Message}");
        }

        if (settingsUpdated)
        {
            cancellationToken.ThrowIfCancellationRequested();
            reporter.Report("Saving updated settings...");
            await _settingsService.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        }

        return new StartupInitializationResult(settings, activeProject, providerDisplayName);
    }

    private static ProjectSettings? ResolveActiveProject(AppSettings settings, ref bool settingsUpdated)
    {
        ProjectSettings? activeProject = null;
        if (!string.IsNullOrWhiteSpace(settings.ActiveProjectId))
        {
            activeProject = settings.Projects.Find(p =>
                string.Equals(p.Id, settings.ActiveProjectId, StringComparison.Ordinal));
        }

        if (activeProject is null && settings.Projects.Count > 0)
        {
            activeProject = settings.Projects[0];
            settings.ActiveProjectId = activeProject.Id;
            settingsUpdated = true;
        }

        return activeProject;
    }
}
