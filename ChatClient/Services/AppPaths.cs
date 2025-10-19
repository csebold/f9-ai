using System;
using System.IO;

namespace ChatClient.Services;

public static class AppPaths
{
    private static string? _baseDirectoryOverride;

    public static string GetBaseDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_baseDirectoryOverride))
        {
            Directory.CreateDirectory(_baseDirectoryOverride);
            return _baseDirectoryOverride;
        }

        var directory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = AppContext.BaseDirectory;
        }

        var root = Path.Combine(directory, "Foundry9", "ChatClient");
        Directory.CreateDirectory(root);
        return root;
    }

    public static string GetSettingsPath()
    {
        var baseDirectory = GetBaseDirectory();
        return Path.Combine(baseDirectory, "settings.json");
    }

    public static string GetSessionStorePath()
    {
        var baseDirectory = GetBaseDirectory();
        return Path.Combine(baseDirectory, "sessions.json");
    }

    public static string GetProjectsRoot()
    {
        var baseDirectory = GetBaseDirectory();
        var projectsRoot = Path.Combine(baseDirectory, "Projects");
        Directory.CreateDirectory(projectsRoot);
        return projectsRoot;
    }

    public static string GetProjectWorkspacePath(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new ArgumentException("Project id must be provided.", nameof(projectId));
        }

        var root = GetProjectsRoot();
        return Path.Combine(root, projectId);
    }

    public static string GetLogsDirectory()
    {
        var baseDirectory = GetBaseDirectory();
        var logsDirectory = Path.Combine(baseDirectory, "Logs");
        Directory.CreateDirectory(logsDirectory);
        return logsDirectory;
    }

    public static string GetOllamaProcessRegistryPath()
    {
        var baseDirectory = GetBaseDirectory();
        return Path.Combine(baseDirectory, "ollama-processes.json");
    }

    public static string GetProviderBrandingDirectory()
    {
        var baseDirectory = GetBaseDirectory();
        var brandingDirectory = Path.Combine(baseDirectory, "Branding");
        Directory.CreateDirectory(brandingDirectory);
        return brandingDirectory;
    }

    public static string GetProviderFaviconPath(LlmProvider provider, string extension = ".ico")
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".ico";
        }

        if (!extension.StartsWith('.'))
        {
            extension = "." + extension;
        }

        var sanitizedExtension = extension.ToLowerInvariant();
        var fileName = $"{provider.ToString().ToLowerInvariant()}{sanitizedExtension}";
        return Path.Combine(GetProviderBrandingDirectory(), fileName);
    }

    internal static IDisposable OverrideBaseDirectoryForTesting(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Override directory must be provided.", nameof(directory));
        }

        var previous = _baseDirectoryOverride;
        _baseDirectoryOverride = directory;
        return new OverrideScope(() => _baseDirectoryOverride = previous);
    }

    private sealed class OverrideScope : IDisposable
    {
        private readonly Action _onDispose;

        public OverrideScope(Action onDispose)
        {
            _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
        }

        public void Dispose() => _onDispose();
    }
}
