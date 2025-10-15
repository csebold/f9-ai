using System;
using Avalonia.Controls;
using ChatClient.Models;
using ChatClient.ViewModels;

namespace ChatClient.Views;

public partial class ProjectEditorWindow : Window
{
    private ProjectEditorViewModel? _viewModel;

    public ProjectEditorWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Saved -= OnSaved;
            _viewModel.Cancelled -= OnCancelled;
        }

        _viewModel = DataContext as ProjectEditorViewModel;
        if (_viewModel is not null)
        {
            _viewModel.Saved += OnSaved;
            _viewModel.Cancelled += OnCancelled;
        }
    }

    private void OnSaved(object? sender, ProjectSettings e) => Close(e);

    private void OnCancelled(object? sender, EventArgs e) => Close(null);
}
