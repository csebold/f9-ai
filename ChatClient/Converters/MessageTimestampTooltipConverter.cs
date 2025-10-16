using System;
using System.Globalization;
using Avalonia.Data.Converters;
using ChatClient.Utilities;

namespace ChatClient.Converters;

public sealed class MessageTimestampTooltipConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTimeOffset timestamp)
        {
            return string.Empty;
        }

        return TimestampFormatter.GetTooltip(timestamp, culture);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
