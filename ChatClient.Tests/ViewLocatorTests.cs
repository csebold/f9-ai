using Avalonia.Controls;

namespace ChatClient.Tests;

public class ViewLocatorTests
{
    [Fact]
    public void Match_ReturnsTrue_ForViewModelBase()
    {
        var locator = new ViewLocator();
        var viewModel = new TestViewModel();

        Assert.True(locator.Match(viewModel));
    }

    [Fact]
    public void Match_ReturnsFalse_ForNonViewModel()
    {
        var locator = new ViewLocator();

        Assert.False(locator.Match(new object()));
    }

    [Fact]
    public void Build_ReturnsNull_WhenParameterIsNull()
    {
        var locator = new ViewLocator();

        var result = locator.Build(null);

        Assert.Null(result);
    }

    [Fact]
    public void Build_ReturnsTextBlock_WhenViewNotFound()
    {
        var locator = new ViewLocator();
        var viewModel = new MissingViewModel();

        var result = locator.Build(viewModel);

        var textBlock = Assert.IsType<TextBlock>(result);
        Assert.Contains("Not Found", textBlock.Text, StringComparison.Ordinal);
    }

    private sealed class TestViewModel : ChatClient.ViewModels.ViewModelBase
    {
    }

    private sealed class MissingViewModel : ChatClient.ViewModels.ViewModelBase
    {
    }
}
