using System;
using System.Globalization;
using ChatClient.Converters;
using ChatClient.Utilities;
using Xunit;

namespace ChatClient.Tests.Converters;

public class MessageTimestampConverterTests
{
    private static readonly CultureInfo TestCulture = CultureInfo.InvariantCulture;

    [Fact]
    public void DisplayConverter_ReturnsEmptyString_WhenValueIsNotTimestamp()
    {
        var converter = new MessageTimestampDisplayConverter();

        var result = converter.Convert(value: null, typeof(string), parameter: null, TestCulture);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void DisplayConverter_FormatsTimestampUsingFormatter()
    {
        var converter = new MessageTimestampDisplayConverter();
        var timestamp = DateTimeOffset.UtcNow;

        var result = converter.Convert(timestamp, typeof(string), parameter: null, TestCulture);
        var expected = TimestampFormatter.GetConversationDisplay(timestamp, DateTimeOffset.Now, TestCulture);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void DisplayConverter_ConvertBackNotSupported()
    {
        var converter = new MessageTimestampDisplayConverter();

        Assert.Throws<NotSupportedException>(() => converter.ConvertBack("value", typeof(DateTimeOffset), null, TestCulture));
    }

    [Fact]
    public void TooltipConverter_ReturnsEmptyString_WhenValueIsNotTimestamp()
    {
        var converter = new MessageTimestampTooltipConverter();

        var result = converter.Convert(value: null, typeof(string), parameter: null, TestCulture);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void TooltipConverter_FormatsTimestampUsingFormatter()
    {
        var converter = new MessageTimestampTooltipConverter();
        var timestamp = DateTimeOffset.UtcNow;

        var result = converter.Convert(timestamp, typeof(string), parameter: null, TestCulture);
        var expected = TimestampFormatter.GetTooltip(timestamp, TestCulture);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void TooltipConverter_ConvertBackNotSupported()
    {
        var converter = new MessageTimestampTooltipConverter();

        Assert.Throws<NotSupportedException>(() => converter.ConvertBack("value", typeof(DateTimeOffset), null, TestCulture));
    }
}
