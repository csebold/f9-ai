using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ChatClient.Models;

namespace ChatClient.Converters;

/// <summary>
/// Converts a message role into a themed background brush.
/// </summary>
public sealed class MessageRoleToBrushConverter : IValueConverter
{
    public IBrush? UserBrush { get; set; }

    public IBrush? AssistantBrush { get; set; }

    public IBrush? SystemBrush { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MessageRole role)
        {
            return role switch
            {
                MessageRole.User => UserBrush,
                MessageRole.Assistant => AssistantBrush,
                MessageRole.System => SystemBrush,
                _ => SystemBrush
            };
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
