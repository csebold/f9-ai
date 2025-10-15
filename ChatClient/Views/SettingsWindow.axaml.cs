using System;
using Avalonia.Controls;
using ChatClient.Models;
using ChatClient.ViewModels;

namespace ChatClient.Views;

public partial class SettingsWindow : Window
{
    private SettingsViewModel? _viewModel;

    public SettingsWindow()
    {
        InitializeComponent();
        HookViewModel();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        HookViewModel();
    }

    private void HookViewModel()
    {
        if (_viewModel is not null)
        {
            _viewModel.Saved -= OnSaved;
            _viewModel.Cancelled -= OnCancelled;
        }

        _viewModel = DataContext as SettingsViewModel;
        if (_viewModel is not null)
        {
            _viewModel.Saved += OnSaved;
            _viewModel.Cancelled += OnCancelled;
        }
    }

    private void OnSaved(object? sender, AppSettings e)
    {
        Close(e);
    }

    private void OnCancelled(object? sender, EventArgs e)
    {
        Close(null);
    }
}
