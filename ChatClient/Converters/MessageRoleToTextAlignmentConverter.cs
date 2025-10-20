using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ChatClient.Models;

namespace ChatClient.Converters;

/// <summary>
/// Maps a message role to a text alignment for content blocks.
/// </summary>
public sealed class MessageRoleToTextAlignmentConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MessageRole role)
        {
            return TextAlignment.Left;
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
