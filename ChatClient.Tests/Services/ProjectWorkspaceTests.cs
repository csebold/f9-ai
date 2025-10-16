using System;
using System.IO;
using ChatClient.Models;
using ChatClient.Services;
using Xunit;

namespace ChatClient.Tests.Services;

public class ProjectWorkspaceTests
{
    [Fact]
    public void EnsureWorkspace_AssignsIdWhenMissing()
    {
        var project = new ProjectSettings
        {
            Id = string.Empty,
            WorkspacePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        };

        ProjectWorkspace.EnsureWorkspace(project);

        Assert.False(string.IsNullOrWhiteSpace(project.Id));
        Assert.True(Directory.Exists(project.WorkspacePath));
    }

    [Fact]
    public void EnsureWorkspace_AssignsWorkspacePathWhenMissing()
    {
        var project = new ProjectSettings
        {
            Id = Guid.NewGuid().ToString("N"),
            WorkspacePath = null
        };

        ProjectWorkspace.EnsureWorkspace(project);

        Assert.False(string.IsNullOrWhiteSpace(project.WorkspacePath));
        Assert.True(Directory.Exists(project.WorkspacePath));
    }

    [Fact]
    public void EnsureWorkspace_CreatesDirectoryForProvidedPath()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var project = new ProjectSettings
        {
            Id = Guid.NewGuid().ToString("N"),
            WorkspacePath = Path.Combine(tempRoot, "Workspace")
        };

        ProjectWorkspace.EnsureWorkspace(project);

        Assert.True(Directory.Exists(project.WorkspacePath));
    }
}
