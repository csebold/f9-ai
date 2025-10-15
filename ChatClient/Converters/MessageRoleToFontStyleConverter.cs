using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ChatClient.Models;

namespace ChatClient.Converters;

/// <summary>
/// Maps a message role to a font style, making system messages italic.
/// </summary>
public sealed class MessageRoleToFontStyleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MessageRole role)
        {
            return role == MessageRole.System ? FontStyle.Italic : FontStyle.Normal;
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
