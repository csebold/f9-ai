using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace ChatClient.Converters;

/// <summary>
/// Scales a numeric value by a configurable fractional factor.
/// </summary>
public sealed class FractionValueConverter : IValueConverter
{
    public double Factor { get; set; } = 1.0;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double number && !double.IsNaN(number))
        {
            return number * Factor;
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double number && !double.IsNaN(number) && Factor != 0)
        {
            return number / Factor;
        }

        return AvaloniaProperty.UnsetValue;
    }
}
