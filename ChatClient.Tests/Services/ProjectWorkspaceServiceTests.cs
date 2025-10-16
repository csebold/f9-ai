using System;
using System.IO;
using ChatClient.Models;
using ChatClient.Services;
using Xunit;

namespace ChatClient.Tests.Services;

public class ProjectWorkspaceServiceTests
{
    [Fact]
    public void EnsureWorkspace_ReturnsFalseWhenPathUnchanged()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var project = new ProjectSettings
        {
            WorkspacePath = tempPath
        };

        var service = new ProjectWorkspaceService();
        var result = service.EnsureWorkspace(project);

        Assert.False(result);
        Assert.Equal(tempPath, project.WorkspacePath);
        Assert.True(Directory.Exists(project.WorkspacePath));
    }

    [Fact]
    public void EnsureWorkspace_ReturnsTrueWhenPathAssigned()
    {
        var project = new ProjectSettings
        {
            Id = Guid.NewGuid().ToString("N"),
            WorkspacePath = null
        };

        var service = new ProjectWorkspaceService();
        var result = service.EnsureWorkspace(project);

        Assert.True(result);
        Assert.False(string.IsNullOrWhiteSpace(project.WorkspacePath));
        Assert.True(Directory.Exists(project.WorkspacePath));
    }
}
