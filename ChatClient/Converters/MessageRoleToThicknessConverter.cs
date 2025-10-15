using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using ChatClient.Models;

namespace ChatClient.Converters;

/// <summary>
/// Maps a message role to an appropriate margin.
/// </summary>
public sealed class MessageRoleToThicknessConverter : IValueConverter
{
    public Thickness SystemMargin { get; set; } = new Thickness(24, 0, 96, 12);

    public Thickness AssistantMargin { get; set; } = new Thickness(0, 0, 96, 12);

    public Thickness UserMargin { get; set; } = new Thickness(96, 0, 24, 12);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MessageRole role)
        {
            return role switch
            {
                MessageRole.System => SystemMargin,
                MessageRole.Assistant => AssistantMargin,
                MessageRole.User => UserMargin,
                _ => AssistantMargin
            };
        }

        return AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
