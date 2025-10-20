using System;
using System.IO;
using System.Threading.Tasks;
using ChatClient.Models;
using ChatClient.Services;
using Xunit;

namespace ChatClient.Tests.Services;

public class ProjectFileServiceTests
{
    [Fact]
    public void GetFiles_ReturnsEmptyWhenDirectoryMissing()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var project = new ProjectSettings
            {
                Id = Guid.NewGuid().ToString("N"),
                WorkspacePath = Path.Combine(tempRoot, "workspace")
            };

            var service = new ProjectFileService();
            var files = service.GetFiles(project);

            Assert.Empty(files);
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public async Task AddFileAsync_CopiesFileAndGeneratesUniqueNames()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var project = new ProjectSettings
            {
                Id = Guid.NewGuid().ToString("N"),
                WorkspacePath = Path.Combine(tempRoot, "workspace")
            };

            var sourceFile = Path.Combine(tempRoot, "source.txt");
            await File.WriteAllTextAsync(sourceFile, "test-content");

            var service = new ProjectFileService();
            var first = await service.AddFileAsync(project, sourceFile);
            var second = await service.AddFileAsync(project, sourceFile);

            Assert.Equal("source.txt", first.Name);
            Assert.True(File.Exists(first.FullPath));
            Assert.NotEqual(first.Name, second.Name);

            var files = service.GetFiles(project);
            Assert.Equal(2, files.Count);
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public async Task DeleteFileAsync_RemovesFile()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            var project = new ProjectSettings
            {
                Id = Guid.NewGuid().ToString("N"),
                WorkspacePath = Path.Combine(tempRoot, "workspace")
            };

            var sourceFile = Path.Combine(tempRoot, "keep.txt");
            await File.WriteAllTextAsync(sourceFile, "keep");

            var service = new ProjectFileService();
            var uploaded = await service.AddFileAsync(project, sourceFile);
            Assert.True(File.Exists(uploaded.FullPath));

            await service.DeleteFileAsync(project, uploaded.Name);
            Assert.False(File.Exists(uploaded.FullPath));
        }
        finally
        {
            DeleteDirectory(tempRoot);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "f9-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // best effort cleanup
        }
    }
}
