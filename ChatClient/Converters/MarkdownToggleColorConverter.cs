using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ChatClient.Converters;

/// <summary>
/// Converts IsShowingMarkdown boolean to a brush color.
/// Red when showing Markdown (true), Green when showing source (false).
/// </summary>
public class MarkdownToggleColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isShowingMarkdown)
        {
            // Red for Markdown mode, Green for source mode
            return isShowingMarkdown 
                ? new SolidColorBrush(Color.FromRgb(220, 50, 47))   // Red
                : new SolidColorBrush(Color.FromRgb(133, 153, 0));  // Green
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
