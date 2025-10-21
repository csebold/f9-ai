using System;
using System.Windows.Input;
using Avalonia.Controls;
using ChatClient.Models;
using ChatClient.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ChatClient.ViewModels;

/// <summary>
/// ViewModel wrapper for a Message that adds Markdown display toggle functionality.
/// </summary>
public partial class MessageViewModel : ViewModelBase
{
    private static MarkdownToAvaloniaRenderer? _sharedRenderer;
    private static MarkdownToAvaloniaRenderer SharedRenderer => _sharedRenderer ??= new MarkdownToAvaloniaRenderer();
    
    private readonly Message _message;
    private bool _isShowingMarkdown = true; // Default to Markdown mode
    private Control? _renderedContent;

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

    /// <summary>
    /// Gets the rendered markdown content as an Avalonia Control.
    /// Lazily rendered on first access and cached.
    /// Returns null if called during design mode or before app initialization.
    /// </summary>
    public Control? RenderedContent
    {
        get
        {
            if (_renderedContent is null)
            {
                // Don't try to create controls if we're in design mode or during XAML compilation
                if (Avalonia.Controls.Design.IsDesignMode || Avalonia.Application.Current is null)
                {
                    return null;
                }
                
                try
                {
                    _renderedContent = SharedRenderer.RenderToControl(Content);
                }
                catch
                {
                    // If rendering fails, return null to prevent crash
                    return null;
                }
            }
            return _renderedContent;
        }
    }

    public ICommand ToggleMarkdownCommand { get; } = null!;

    private void ToggleMarkdown()
    {
        IsShowingMarkdown = !IsShowingMarkdown;
    }
}
