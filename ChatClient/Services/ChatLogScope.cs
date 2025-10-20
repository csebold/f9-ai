using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ChatClient.Services;

/// <summary>
/// Provides ambient access to the active chat log during asynchronous operations.
/// </summary>
public static class ChatLogScope
{
    private static readonly AsyncLocal<ChatLogHandle?> CurrentHandle = new();

    public static ChatLogHandle? Current => CurrentHandle.Value;

    public static IDisposable Push(ChatLogHandle? handle)
    {
        var previous = CurrentHandle.Value;
        CurrentHandle.Value = handle;
        return new Scope(previous);
    }

    public static Task LogAsync(
        string kind,
        string message,
        IEnumerable<string>? metadata = null,
        string? body = null,
        bool isBinary = false)
    {
        var handle = CurrentHandle.Value;
        if (handle is null)
        {
            return Task.CompletedTask;
        }

        return handle.AppendAsync(kind, message, metadata, body, isBinary);
    }

    private sealed class Scope : IDisposable
    {
        private readonly ChatLogHandle? _previous;
        private bool _disposed;

        public Scope(ChatLogHandle? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CurrentHandle.Value = _previous;
            _disposed = true;
        }
    }
}
