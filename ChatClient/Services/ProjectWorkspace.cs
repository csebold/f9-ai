using System;
using System.IO;
using ChatClient.Models;

namespace ChatClient.Services;

public static class ProjectWorkspace
{
    public static void EnsureWorkspace(ProjectSettings project)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        if (string.IsNullOrWhiteSpace(project.Id))
        {
            project.Id = Guid.NewGuid().ToString("N");
        }

        if (string.IsNullOrWhiteSpace(project.WorkspacePath))
        {
            project.WorkspacePath = AppPaths.GetProjectWorkspacePath(project.Id);
        }

        Directory.CreateDirectory(project.WorkspacePath);
    }

    public static string GetProjectFilesDirectory(ProjectSettings project)
    {
        EnsureWorkspace(project);

        var filesDirectory = Path.Combine(project.WorkspacePath!, "files");
        Directory.CreateDirectory(filesDirectory);
        return filesDirectory;
    }

    public static string GetChatFilesDirectory(ProjectSettings project, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Session id must be provided.", nameof(sessionId));
        }

        EnsureWorkspace(project);

        var chatDirectory = Path.Combine(project.WorkspacePath!, "chats", sessionId, "files");
        Directory.CreateDirectory(chatDirectory);
        return chatDirectory;
    }
}
