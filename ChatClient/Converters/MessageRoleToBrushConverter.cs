using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using ChatClient.Models;

namespace ChatClient.Converters;

/// <summary>
/// Converts a message role into a themed background brush.
/// </summary>
public sealed class MessageRoleToBrushConverter : IValueConverter
{
    public string UserBrushKey { get; set; } = "UserMessageBackgroundBrush";

    public string AssistantBrushKey { get; set; } = "AssistantMessageBackgroundBrush";

    public string SystemBrushKey { get; set; } = "SystemMessageBackgroundBrush";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MessageRole role)
        {
            var resourceKey = role switch
            {
                MessageRole.User => UserBrushKey,
                MessageRole.Assistant => AssistantBrushKey,
                MessageRole.System => SystemBrushKey,
                _ => SystemBrushKey
            };

            var theme = Application.Current?.ActualThemeVariant ?? ThemeVariant.Default;
            if (Application.Current?.TryGetResource(resourceKey, theme, out var brush) == true &&
                brush is IBrush typedBrush)
            {
                return typedBrush;
            }
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
