using System;
using System.Globalization;
using Avalonia.Data.Converters;
using ChatClient.Utilities;

namespace ChatClient.Converters;

public sealed class MessageTimestampDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTimeOffset timestamp)
        {
            return string.Empty;
        }

        return TimestampFormatter.GetConversationDisplay(timestamp, DateTimeOffset.Now, culture);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
