using System;
using System.Windows.Input;
using ChatClient.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChatClient.ViewModels;

/// <summary>
/// ViewModel wrapper for a Message that adds Markdown display toggle functionality.
/// </summary>
public partial class MessageViewModel : ViewModelBase
{
    private readonly Message _message;
    private bool _isShowingMarkdown = true; // Default to Markdown mode

    public MessageViewModel(Message message)
    {
        _message = message;
        ToggleMarkdownCommand = new RelayCommand(ToggleMarkdown);
    }

    public Message UnderlyingMessage => _message;

    public string Author => _message.Author;
    public string Content => _message.Content;
    public DateTimeOffset Timestamp => _message.Timestamp;
    public MessageRole Role => _message.Role;
    public bool IsUser => _message.IsUser;
    public bool IsSystem => _message.IsSystem;
    public bool IsAssistant => _message.IsAssistant;

    public bool IsShowingMarkdown
    {
        get => _isShowingMarkdown;
        set
        {
            if (_isShowingMarkdown != value)
            {
                _isShowingMarkdown = value;
                OnPropertyChanged(nameof(IsShowingMarkdown));
            }
        }
    }

    public ICommand ToggleMarkdownCommand { get; } = null!;

    private void ToggleMarkdown()
    {
        IsShowingMarkdown = !IsShowingMarkdown;
    }
}
