using System.Globalization;
using ChatClient.Converters;
using Xunit;

namespace ChatClient.Tests.Converters;

public sealed class BooleanNegationConverterTests
{
    private static readonly BooleanNegationConverter Converter = new();

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Convert_InvertsBoolean(bool input, bool expected)
    {
        var result = Converter.Convert(input, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_ReturnsUnset_ForNonBoolean()
    {
        var result = Converter.Convert("nope", typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Same(Avalonia.AvaloniaProperty.UnsetValue, result);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ConvertBack_InvertsBoolean(bool input, bool expected)
    {
        var result = Converter.ConvertBack(input, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConvertBack_ReturnsUnset_ForNonBoolean()
    {
        var result = Converter.ConvertBack(5, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Same(Avalonia.AvaloniaProperty.UnsetValue, result);
    }
}
