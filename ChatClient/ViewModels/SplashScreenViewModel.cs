using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ChatClient.ViewModels;

public partial class SplashScreenViewModel : ObservableObject
{
    public ObservableCollection<string> StatusMessages { get; } = new();

    public void AddStatus(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var timestamp = DateTimeOffset.Now.ToString("HH:mm:ss");
        StatusMessages.Add($"[{timestamp}] {message}");
    }
}
