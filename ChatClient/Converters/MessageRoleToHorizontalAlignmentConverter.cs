using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using ChatClient.Models;

namespace ChatClient.Converters;

/// <summary>
/// Maps a message role to a horizontal alignment for layout.
/// </summary>
public sealed class MessageRoleToHorizontalAlignmentConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MessageRole role)
        {
            return role == MessageRole.User ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
