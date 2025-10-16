using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace ChatClient.Converters;

/// <summary>
/// Inverts a boolean value for binding scenarios.
/// </summary>
public sealed class BooleanNegationConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool flag)
        {
            return !flag;
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool flag)
        {
            return !flag;
        }

        return AvaloniaProperty.UnsetValue;
    }
}
