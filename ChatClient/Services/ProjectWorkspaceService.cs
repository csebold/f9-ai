using System;
using ChatClient.Models;

namespace ChatClient.Services;

public sealed class ProjectWorkspaceService : IProjectWorkspaceService
{
    public bool EnsureWorkspace(ProjectSettings project)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        var previousPath = project.WorkspacePath;
        ProjectWorkspace.EnsureWorkspace(project);

        return !string.Equals(previousPath, project.WorkspacePath, StringComparison.Ordinal);
    }
}
