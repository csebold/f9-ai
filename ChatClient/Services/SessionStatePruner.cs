using System;
using System.Collections.Generic;
using System.Linq;
using ChatClient.Models;

namespace ChatClient.Services;

public static class SessionStatePruner
{
    public static void Trim(SessionStoreSnapshot snapshot, int maxSessionsPerProject, int maxMessagesPerSession)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var sessionLimit = Math.Max(1, maxSessionsPerProject);
        var messageLimit = Math.Max(1, maxMessagesPerSession);

        foreach (var project in snapshot.Projects)
        {
            var orderedSessions = project.Sessions
                .OrderByDescending(s => s.UpdatedAt)
                .ToList();

            ChatSessionSnapshot? activeSession = null;
            if (!string.IsNullOrWhiteSpace(project.ActiveSessionId))
            {
                activeSession = orderedSessions.FirstOrDefault(s => string.Equals(s.Id, project.ActiveSessionId, StringComparison.Ordinal));
            }

            var trimmedSessions = new List<ChatSessionSnapshot>();
            if (activeSession is not null)
            {
                trimmedSessions.Add(activeSession);
            }

            foreach (var session in orderedSessions)
            {
                if (trimmedSessions.Count >= sessionLimit)
                {
                    break;
                }

                if (trimmedSessions.Contains(session))
                {
                    continue;
                }

                trimmedSessions.Add(session);
            }

            if (trimmedSessions.Count == 0 && orderedSessions.Count > 0)
            {
                trimmedSessions.Add(orderedSessions[0]);
            }

            project.Sessions = trimmedSessions;

            if (!trimmedSessions.Any())
            {
                continue;
            }

            if (activeSession is null || !trimmedSessions.Contains(activeSession))
            {
                project.ActiveSessionId = trimmedSessions[0].Id;
            }

            foreach (var session in trimmedSessions)
            {
                if (session.Messages.Count <= messageLimit)
                {
                    continue;
                }

                var skip = Math.Max(0, session.Messages.Count - messageLimit);
                session.Messages = session.Messages.Skip(skip).ToList();
                session.CreatedAt = session.Messages.FirstOrDefault()?.Timestamp ?? session.CreatedAt;
            }
        }
    }
}
