using System;
using System.Globalization;
using ChatClient.Utilities;
using Xunit;

namespace ChatClient.Tests.Utilities;

public class TimestampFormatterTests
{
    private static readonly CultureInfo TestCulture = CultureInfo.InvariantCulture;

    [Fact]
    public void GetConversationDisplay_ReturnsRelativeAndAbsolute_WhenRecent()
    {
        var timestamp = new DateTimeOffset(2024, 01, 01, 10, 0, 0, TimeSpan.Zero);
        var reference = timestamp.AddMinutes(5);

        var absolute = TimestampFormatter.GetAbsolute(timestamp, TestCulture);

        var result = TimestampFormatter.GetConversationDisplay(timestamp, reference, TestCulture);

        Assert.Equal($"5 minutes ago • {absolute}", result);
    }

    [Fact]
    public void GetConversationDisplay_ReturnsAbsolute_WhenOlderThanThreshold()
    {
        var timestamp = new DateTimeOffset(2024, 01, 01, 10, 0, 0, TimeSpan.Zero);
        var reference = timestamp.AddHours(13);

        var expected = TimestampFormatter.GetAbsolute(timestamp, TestCulture);
        var result = TimestampFormatter.GetConversationDisplay(timestamp, reference, TestCulture);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetConversationDisplay_ReturnsJustNowForFutureTimestamps()
    {
        var timestamp = new DateTimeOffset(2024, 01, 01, 10, 0, 0, TimeSpan.Zero);
        var reference = timestamp.AddMinutes(-1);
        var absolute = TimestampFormatter.GetAbsolute(timestamp, TestCulture);

        var result = TimestampFormatter.GetConversationDisplay(timestamp, reference, TestCulture);

        Assert.Equal($"just now • {absolute}", result);
    }

    [Fact]
    public void GetAbsolute_UsesShortTimeForSameDay()
    {
        var localNow = DateTimeOffset.Now;
        var timestamp = new DateTimeOffset(
            localNow.Year,
            localNow.Month,
            localNow.Day,
            12,
            0,
            0,
            localNow.Offset);

        var result = TimestampFormatter.GetAbsolute(timestamp, TestCulture);

        Assert.Equal(timestamp.ToLocalTime().ToString("t", TestCulture), result);
    }

    [Fact]
    public void GetAbsolute_UsesGeneralFormatForDifferentDay()
    {
        var timestamp = DateTimeOffset.Now.AddDays(-2);

        var result = TimestampFormatter.GetAbsolute(timestamp, TestCulture);

        Assert.Equal(timestamp.ToLocalTime().ToString("g", TestCulture), result);
    }

    [Fact]
    public void GetTooltip_FormatsFullTimestamp()
    {
        var timestamp = new DateTimeOffset(2024, 01, 01, 10, 0, 0, TimeSpan.Zero);

        var result = TimestampFormatter.GetTooltip(timestamp, TestCulture);

        Assert.Equal(timestamp.ToLocalTime().ToString("F", TestCulture), result);
    }

    [Fact]
    public void GetSessionSubtitle_ReturnsRelative_WhenWithinADay()
    {
        var timestamp = new DateTimeOffset(2024, 01, 01, 10, 0, 0, TimeSpan.Zero);
        var reference = timestamp.AddHours(3);

        var result = TimestampFormatter.GetSessionSubtitle(timestamp, reference, TestCulture);

        Assert.Equal("3 hours ago", result);
    }

    [Fact]
    public void GetSessionSubtitle_ReturnsAbsolute_WhenOlderThanDay()
    {
        var timestamp = new DateTimeOffset(2024, 01, 01, 10, 0, 0, TimeSpan.Zero);
        var reference = timestamp.AddDays(2);

        var expected = TimestampFormatter.GetAbsolute(timestamp, TestCulture);
        var result = TimestampFormatter.GetSessionSubtitle(timestamp, reference, TestCulture);

        Assert.Equal(expected, result);
    }
}
