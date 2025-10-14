using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace ChatClient.Converters;

/// <summary>
/// Converts a window height to half its value so controls can cap their growth.
/// </summary>
public sealed class HalfValueConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double height && !double.IsNaN(height))
        {
            return height / 2d;
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double halfHeight && !double.IsNaN(halfHeight))
        {
            return halfHeight * 2d;
        }

        return AvaloniaProperty.UnsetValue;
    }
}
