using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ChatClient.Services;
using Xunit;

namespace ChatClient.Tests.Services;

public class ChatLogServiceTests
{
    [Fact]
    public async Task ChatLogHandle_AppendsEntriesAndReadsTail()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"chatlog-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var service = new ChatLogService();
            var logPath = Path.Combine(tempRoot, "session-1.log");
            var handle = service.GetOrCreate("session-1", logPath);

            var appended = new List<string>();
            handle.EntryAppended += (_, entry) => appended.Add(entry);

            await handle.AppendAsync("INFO", "Session started", null, null);
            await handle.AppendAsync("SEND", "Test request", new[] { "Meta: value" }, "{ \"value\": 1 }");

            var history = handle.ReadRecentEntries(10);

            Assert.Equal(2, history.Count);
            Assert.Equal(2, appended.Count);
            Assert.Contains("Session started", history[0]);
            Assert.Contains("Test request", history[1]);
            Assert.Contains("{ \"value\": 1 }", history[1]);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Fact]
    public async Task ChatLogScope_WritesToCurrentHandle()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"chatlog-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var service = new ChatLogService();
            var logPath = Path.Combine(tempRoot, "session-2.log");
            var handle = service.GetOrCreate("session-2", logPath);

            using (ChatLogScope.Push(handle))
            {
                await ChatLogScope.LogAsync("INFO", "Scoped entry", null, "Body");
            }

            var history = handle.ReadRecentEntries(5);

            Assert.Single(history);
            Assert.Contains("Scoped entry", history[0]);
            Assert.Contains("Body", history[0]);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // best effort cleanup for test artifacts
        }
    }
}
