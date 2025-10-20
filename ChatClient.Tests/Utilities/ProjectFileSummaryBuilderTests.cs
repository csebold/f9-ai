using System;
using System.IO;
using ChatClient.Utilities;
using Xunit;

namespace ChatClient.Tests.Utilities;

public class ProjectFileSummaryBuilderTests
{
    [Fact]
    public void Build_ReturnsEmpty_WhenDirectoryMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var summary = ProjectFileSummaryBuilder.Build(path);

        Assert.Equal(string.Empty, summary);
    }

    [Fact]
    public void Build_ListsVisibleFilesAndSkipsExcludedDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), "f9-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var src = Path.Combine(root, "src");
            Directory.CreateDirectory(src);

            File.WriteAllText(Path.Combine(root, "README.md"), "readme");
            File.WriteAllText(Path.Combine(src, "Program.cs"), "class Program {}");

            var bin = Path.Combine(root, "bin");
            Directory.CreateDirectory(bin);
            File.WriteAllText(Path.Combine(bin, "ignored.dll"), "binary");

            var summary = ProjectFileSummaryBuilder.Build(root, maxFiles: 10);

            Assert.Contains("Project files available (2 files).", summary);
            Assert.Contains("- README.md", summary);
            Assert.Contains("- src/Program.cs", summary);
            Assert.DoesNotContain("ignored.dll", summary);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // best effort cleanup
            }
        }
    }

    [Fact]
    public void Build_UsesScopeNameWhenProvided()
    {
        var root = Path.Combine(Path.GetTempPath(), "f9-chat-summary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            File.WriteAllText(Path.Combine(root, "notes.txt"), "hello");

            var summary = ProjectFileSummaryBuilder.Build(root, scopeName: "Chat");

            Assert.StartsWith("Chat files available", summary);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // best effort cleanup
            }
        }
    }
}
