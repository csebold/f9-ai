using System;
using System.IO;

namespace ChatClient.Services;

public static class AppPaths
{
    public static string GetBaseDirectory()
    {
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
}
