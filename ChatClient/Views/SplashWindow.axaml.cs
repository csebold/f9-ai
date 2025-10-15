using System;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace ChatClient.Views;

public partial class SplashWindow : Window
{
    private readonly DispatcherTimer _animationTimer;
    private readonly RotateTransform _gearTransform;
    private double _currentAngle;
    private DateTime _lastTick;

    public SplashWindow()
    {
        InitializeComponent();

        var gearBorder = this.FindControl<Border>("GearBorder");
        var renderTransform = gearBorder?.RenderTransform as RotateTransform;
        if (renderTransform is null)
        {
            renderTransform = new RotateTransform();
            if (gearBorder is not null)
            {
                gearBorder.RenderTransform = renderTransform;
            }
        }

        _gearTransform = renderTransform;
        _lastTick = DateTime.UtcNow;

        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _animationTimer.Stop();
        _animationTimer.Tick -= OnAnimationTick;
        base.OnClosed(e);
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var deltaSeconds = (now - _lastTick).TotalSeconds;
        _lastTick = now;

        const double rotationsPerSecond = 0.5; // half rotation per second
        _currentAngle = (_currentAngle + 360 * rotationsPerSecond * deltaSeconds) % 360;
        _gearTransform.Angle = _currentAngle;
    }
}
