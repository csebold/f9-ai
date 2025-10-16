using System;
using System.Linq;
using ChatClient.Models;
using ChatClient.Services;
using Xunit;

namespace ChatClient.Tests.Services;

public class SessionStatePrunerTests
{
    [Fact]
    public void Trim_KeepsActiveSessionAndLimitsCounts()
    {
        var baseTime = DateTimeOffset.UtcNow;
        var snapshot = new SessionStoreSnapshot
        {
            Projects =
            {
                new ProjectSessionSnapshot
                {
                    ProjectId = "alpha",
                    ActiveSessionId = "session-3",
                    Sessions =
                    {
                        CreateSession("session-1", baseTime.AddMinutes(-30), 5),
                        CreateSession("session-2", baseTime.AddMinutes(-10), 3),
                        CreateSession("session-3", baseTime.AddMinutes(-20), 4)
                    }
                }
            }
        };

        SessionStatePruner.Trim(snapshot, maxSessionsPerProject: 2, maxMessagesPerSession: 2);

        var project = Assert.Single(snapshot.Projects);
        Assert.Equal(2, project.Sessions.Count);
        Assert.Contains(project.Sessions, s => s.Id == "session-3");
        Assert.Equal("session-3", project.ActiveSessionId);
        Assert.All(project.Sessions, s => Assert.InRange(s.Messages.Count, 1, 2));
    }

    [Fact]
    public void Trim_AssignsActiveSessionWhenPreviousMissing()
    {
        var baseTime = DateTimeOffset.UtcNow;
        var snapshot = new SessionStoreSnapshot
        {
            Projects =
            {
                new ProjectSessionSnapshot
                {
                    ProjectId = "beta",
                    ActiveSessionId = "missing",
                    Sessions =
                    {
                        CreateSession("a", baseTime.AddMinutes(-5), 1),
                        CreateSession("b", baseTime.AddMinutes(-1), 1)
                    }
                }
            }
        };

        SessionStatePruner.Trim(snapshot, maxSessionsPerProject: 1, maxMessagesPerSession: 5);

        var project = Assert.Single(snapshot.Projects);
        Assert.Single(project.Sessions);
        var retained = project.Sessions[0];
        Assert.Equal(retained.Id, project.ActiveSessionId);
    }

    private static ChatSessionSnapshot CreateSession(string id, DateTimeOffset updatedAt, int messageCount)
    {
        var session = new ChatSessionSnapshot
        {
            Id = id,
            Title = id,
            CreatedAt = updatedAt.AddMinutes(-5),
            UpdatedAt = updatedAt
        };

        for (var i = 0; i < messageCount; i++)
        {
            session.Messages.Add(new Message("User", $"Message {i}", updatedAt.AddSeconds(i), MessageRole.User));
        }

        return session;
    }
}
