using System;
using ChatClient.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ChatClient.ViewModels;

public sealed partial class ProjectFileItemViewModel : ObservableObject
{
    private readonly ProjectFile _file;

    public ProjectFileItemViewModel(ProjectFile file)
    {
        _file = file ?? throw new ArgumentNullException(nameof(file));
        Name = file.Name;
        FullPath = file.FullPath;
        Size = file.Length;
        LastModifiedUtc = file.LastModifiedUtc;
        SizeDescription = FormatSize(file.Length);
        LastModifiedDescription = FormatTimestamp(file.LastModifiedUtc);
    }

    public string Name { get; }

    public string FullPath { get; }

    public long Size { get; }

    public DateTimeOffset LastModifiedUtc { get; }

    [ObservableProperty]
    private string _sizeDescription;

    [ObservableProperty]
    private string _lastModifiedDescription;

    public ProjectFile Snapshot => _file;

    private static string FormatSize(long bytes)
    {
        const long OneKB = 1024;
        const long OneMB = OneKB * 1024;
        const long OneGB = OneMB * 1024;

        if (bytes >= OneGB)
        {
            return $"{bytes / (double)OneGB:F1} GB";
        }

        if (bytes >= OneMB)
        {
            return $"{bytes / (double)OneMB:F1} MB";
        }

        if (bytes >= OneKB)
        {
            return $"{bytes / (double)OneKB:F1} KB";
        }

        return $"{bytes} B";
    }

    private static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToLocalTime().ToString("g");
}
