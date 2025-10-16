using ChatClient.Models;

namespace ChatClient.Services;

public interface IProjectWorkspaceService
{
    /// <summary>
    /// Ensures the workspace for the provided project exists.
    /// </summary>
    /// <param name="project">The project to validate.</param>
    /// <returns>
    /// True when the workspace path was modified (e.g. newly created). False when no change was required.
    /// </returns>
    bool EnsureWorkspace(ProjectSettings project);
}
