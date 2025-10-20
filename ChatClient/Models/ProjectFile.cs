using System;

namespace ChatClient.Models;

public sealed class ProjectFile
{
    public ProjectFile(string name, string fullPath, long length, DateTimeOffset lastModifiedUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("File name must be provided.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(fullPath))
        {
            throw new ArgumentException("File path must be provided.", nameof(fullPath));
        }

        Name = name;
        FullPath = fullPath;
        Length = length;
        LastModifiedUtc = lastModifiedUtc;
    }

    public string Name { get; }

    public string FullPath { get; }

    public long Length { get; }

    public DateTimeOffset LastModifiedUtc { get; }
}
