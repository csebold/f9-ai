using System;

namespace ChatClient.Services;

public enum BackgroundProcessChangeKind
{
    Added,
    Updated,
    Removed
}

public sealed class BackgroundProcessChangedEventArgs : EventArgs
{
    public BackgroundProcessChangedEventArgs(BackgroundProcessChangeKind kind, BackgroundProcessSnapshot snapshot)
    {
        Kind = kind;
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public BackgroundProcessChangeKind Kind { get; }

    public BackgroundProcessSnapshot Snapshot { get; }
}
