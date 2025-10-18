using System.Linq;
using ChatClient.ViewModels;

namespace ChatClient.Tests.ViewModels;

public class SplashScreenViewModelTests
{
    [Fact]
    public void AddStatus_AppendsTimestampedMessage()
    {
        var viewModel = new SplashScreenViewModel();

        viewModel.AddStatus("Loading assets");

        Assert.Single(viewModel.StatusMessages);
        var message = viewModel.StatusMessages.Single();
        Assert.StartsWith("[", message, StringComparison.Ordinal);
        Assert.EndsWith("Loading assets", message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddStatus_IgnoresBlankMessages()
    {
        var viewModel = new SplashScreenViewModel();

        viewModel.AddStatus(" ");

        Assert.Empty(viewModel.StatusMessages);
    }

    [Fact]
    public void OfferCancellation_EnablesCancel()
    {
        var viewModel = new SplashScreenViewModel();

        viewModel.OfferCancellation("You can cancel now");

        Assert.True(viewModel.IsCancellationOffered);
        Assert.True(viewModel.IsCancelEnabled);
        Assert.True(viewModel.CanCancel);
        Assert.Equal("You can cancel now", viewModel.CancellationMessage);
    }

    [Fact]
    public void OfferCancellation_IgnoresBlankMessage()
    {
        var viewModel = new SplashScreenViewModel();

        viewModel.OfferCancellation("");

        Assert.False(viewModel.IsCancellationOffered);
        Assert.False(viewModel.IsCancelEnabled);
        Assert.Null(viewModel.CancellationMessage);
    }

    [Fact]
    public void CancelCommand_RaisesEvent_WhenEnabled()
    {
        var viewModel = new SplashScreenViewModel();
        viewModel.OfferCancellation("Cancel?");

        var cancelRaised = false;
        viewModel.CancelRequested += (_, _) => cancelRaised = true;

        viewModel.CancelCommand.Execute(null);

        Assert.True(cancelRaised);
    }

    [Fact]
    public void MarkCancellationInProgress_DisablesCancel()
    {
        var viewModel = new SplashScreenViewModel();
        viewModel.OfferCancellation("Cancel?");

        viewModel.MarkCancellationInProgress();

        Assert.Equal("Cancelling startup...", viewModel.CancellationMessage);
        Assert.False(viewModel.IsCancelEnabled);
        Assert.False(viewModel.CanCancel);
    }

    [Fact]
    public void ClearCancellationOffer_ResetsProperties()
    {
        var viewModel = new SplashScreenViewModel();
        viewModel.OfferCancellation("Cancel?");

        viewModel.ClearCancellationOffer();

        Assert.False(viewModel.IsCancellationOffered);
        Assert.False(viewModel.IsCancelEnabled);
        Assert.Null(viewModel.CancellationMessage);
    }
}
