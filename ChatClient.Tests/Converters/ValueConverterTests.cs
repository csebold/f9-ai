using System;
using System.Globalization;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Media;
using ChatClient.Converters;
using ChatClient.Models;
using Xunit;

namespace ChatClient.Tests.Converters;

public class ValueConverterTests
{
    [Fact]
    public void FractionValueConverter_ConvertsAndConvertsBack()
    {
        var converter = new FractionValueConverter { Factor = 0.5 };
        var culture = CultureInfo.InvariantCulture;

        var forward = converter.Convert(10d, typeof(double), null, culture);
        var backward = converter.ConvertBack(5d, typeof(double), null, culture);
        var invalid = converter.Convert("not-a-number", typeof(double), null, culture);

        Assert.Equal(5d, forward);
        Assert.Equal(10d, backward);
        Assert.Same(AvaloniaProperty.UnsetValue, invalid);
    }

    [Fact]
    public void HalfValueConverter_ConvertsAndConvertsBack()
    {
        var converter = new HalfValueConverter();
        var culture = CultureInfo.InvariantCulture;

        Assert.Equal(10d, converter.Convert(20d, typeof(double), null, culture));
        Assert.Equal(20d, converter.ConvertBack(10d, typeof(double), null, culture));
        Assert.Same(AvaloniaProperty.UnsetValue, converter.Convert(null, typeof(double), null, culture));
    }

    [Fact]
    public void MessageRoleToBrushConverter_MapsRolesToBrushes()
    {
        var userBrush = new SolidColorBrush(Colors.Red);
        var assistantBrush = new SolidColorBrush(Colors.Green);
        var systemBrush = new SolidColorBrush(Colors.Blue);
        var converter = new MessageRoleToBrushConverter
        {
            UserBrush = userBrush,
            AssistantBrush = assistantBrush,
            SystemBrush = systemBrush
        };
        var culture = CultureInfo.InvariantCulture;

        Assert.Same(userBrush, converter.Convert(MessageRole.User, typeof(IBrush), null, culture));
        Assert.Same(assistantBrush, converter.Convert(MessageRole.Assistant, typeof(IBrush), null, culture));
        Assert.Same(systemBrush, converter.Convert(MessageRole.System, typeof(IBrush), null, culture));
        Assert.Same(systemBrush, converter.Convert((MessageRole)42, typeof(IBrush), null, culture));
        Assert.Same(AvaloniaProperty.UnsetValue, converter.Convert("invalid", typeof(IBrush), null, culture));
        Assert.Throws<NotSupportedException>(() => converter.ConvertBack(null, typeof(MessageRole), null, culture));
    }

    [Theory]
    [InlineData(MessageRole.System, FontStyle.Italic)]
    [InlineData(MessageRole.User, FontStyle.Normal)]
    [InlineData(MessageRole.Assistant, FontStyle.Normal)]
    public void MessageRoleToFontStyleConverter_MapsStyles(MessageRole role, FontStyle expected)
    {
        var converter = new MessageRoleToFontStyleConverter();
        Assert.Equal(expected, converter.Convert(role, typeof(FontStyle), null, CultureInfo.InvariantCulture));
        Assert.Throws<NotSupportedException>(() => converter.ConvertBack(FontStyle.Italic, typeof(MessageRole), null, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(MessageRole.User, HorizontalAlignment.Right)]
    [InlineData(MessageRole.Assistant, HorizontalAlignment.Left)]
    [InlineData(MessageRole.System, HorizontalAlignment.Left)]
    public void MessageRoleToHorizontalAlignmentConverter_MapsAlignment(MessageRole role, HorizontalAlignment expected)
    {
        var converter = new MessageRoleToHorizontalAlignmentConverter();
        Assert.Equal(expected, converter.Convert(role, typeof(HorizontalAlignment), null, CultureInfo.InvariantCulture));
        Assert.Throws<NotSupportedException>(() => converter.ConvertBack(HorizontalAlignment.Left, typeof(MessageRole), null, CultureInfo.InvariantCulture));
        Assert.Same(AvaloniaProperty.UnsetValue, converter.Convert("invalid", typeof(HorizontalAlignment), null, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(MessageRole.User, TextAlignment.Right)]
    [InlineData(MessageRole.Assistant, TextAlignment.Left)]
    [InlineData(MessageRole.System, TextAlignment.Left)]
    public void MessageRoleToTextAlignmentConverter_MapsAlignment(MessageRole role, TextAlignment expected)
    {
        var converter = new MessageRoleToTextAlignmentConverter();
        Assert.Equal(expected, converter.Convert(role, typeof(TextAlignment), null, CultureInfo.InvariantCulture));
        Assert.Throws<NotSupportedException>(() => converter.ConvertBack(TextAlignment.Left, typeof(MessageRole), null, CultureInfo.InvariantCulture));
        Assert.Same(AvaloniaProperty.UnsetValue, converter.Convert("invalid", typeof(TextAlignment), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void MessageRoleToThicknessConverter_UsesConfiguredMargins()
    {
        var converter = new MessageRoleToThicknessConverter
        {
            SystemMargin = new Thickness(1, 2, 3, 4),
            AssistantMargin = new Thickness(4, 3, 2, 1),
            UserMargin = new Thickness(8, 7, 6, 5)
        };
        var culture = CultureInfo.InvariantCulture;

        Assert.Equal(converter.SystemMargin, converter.Convert(MessageRole.System, typeof(Thickness), null, culture));
        Assert.Equal(converter.AssistantMargin, converter.Convert(MessageRole.Assistant, typeof(Thickness), null, culture));
        Assert.Equal(converter.UserMargin, converter.Convert(MessageRole.User, typeof(Thickness), null, culture));
        Assert.Equal(converter.AssistantMargin, converter.Convert((MessageRole)123, typeof(Thickness), null, culture));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("text", true)]
    public void StringNotEmptyToBoolConverter_ValidatesContent(string input, bool expected)
    {
        var converter = StringNotEmptyToBoolConverter.Instance;
        Assert.Equal(expected, converter.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture));
    }
}
