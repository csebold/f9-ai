using ChatClient.Models;

namespace ChatClient.Services;

public sealed class StartupInitializationResult
{
    public StartupInitializationResult(AppSettings settings, ProjectSettings? activeProject, string providerDisplayName)
    {
        Settings = settings;
        ActiveProject = activeProject;
        ProviderDisplayName = providerDisplayName;
    }

    public AppSettings Settings { get; }

    public ProjectSettings? ActiveProject { get; }

    public string ProviderDisplayName { get; }
}
