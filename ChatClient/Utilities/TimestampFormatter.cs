using System;
using System.Globalization;

namespace ChatClient.Utilities;

/// <summary>
/// Centralized helpers for formatting timestamps so that relative messaging
/// and absolute local times stay consistent across the UI.
/// </summary>
public static class TimestampFormatter
{
    private static readonly TimeSpan RecentThreshold = TimeSpan.FromHours(12);

    public static string GetConversationDisplay(DateTimeOffset timestamp, DateTimeOffset? reference = null, CultureInfo? culture = null)
    {
        var local = timestamp.ToLocalTime();
        var now = reference?.ToLocalTime() ?? DateTimeOffset.Now;
        var absolute = GetAbsolute(local, culture);

        if ((now - local) <= RecentThreshold)
        {
            var relative = GetRelative(now - local, culture);
            return string.IsNullOrWhiteSpace(relative) ? absolute : $"{relative} • {absolute}";
        }

        return absolute;
    }

    public static string GetAbsolute(DateTimeOffset timestamp, CultureInfo? culture = null)
    {
        var local = timestamp.ToLocalTime();
        var targetCulture = culture ?? CultureInfo.CurrentCulture;

        return local.Date == DateTimeOffset.Now.Date
            ? local.ToString("t", targetCulture)
            : local.ToString("g", targetCulture);
    }

    public static string GetTooltip(DateTimeOffset timestamp, CultureInfo? culture = null)
    {
        var targetCulture = culture ?? CultureInfo.CurrentCulture;
        return timestamp.ToLocalTime().ToString("F", targetCulture);
    }

    public static string GetSessionSubtitle(DateTimeOffset timestamp, DateTimeOffset? reference = null, CultureInfo? culture = null)
    {
        var local = timestamp.ToLocalTime();
        var now = reference?.ToLocalTime() ?? DateTimeOffset.Now;

        if ((now - local) <= TimeSpan.FromDays(1))
        {
            var relative = GetRelative(now - local, culture);
            if (!string.IsNullOrWhiteSpace(relative))
            {
                return relative;
            }
        }

        return GetAbsolute(local, culture);
    }

    private static string GetRelative(TimeSpan delta, CultureInfo? culture)
    {
        if (delta <= TimeSpan.Zero)
        {
            return "just now";
        }

        if (delta < TimeSpan.FromSeconds(45))
        {
            return "just now";
        }

        if (delta < TimeSpan.FromMinutes(1.5))
        {
            return "1 minute ago";
        }

        if (delta < TimeSpan.FromHours(1))
        {
            var minutes = Math.Max(1, (int)Math.Round(delta.TotalMinutes));
            return $"{minutes} minute{(minutes == 1 ? string.Empty : "s")} ago";
        }

        if (delta < TimeSpan.FromHours(1.5))
        {
            return "1 hour ago";
        }

        if (delta < TimeSpan.FromDays(1))
        {
            var hours = Math.Max(1, (int)Math.Round(delta.TotalHours));
            return $"{hours} hour{(hours == 1 ? string.Empty : "s")} ago";
        }

        if (delta < TimeSpan.FromDays(2))
        {
            return "yesterday";
        }

        if (delta < TimeSpan.FromDays(7))
        {
            var days = Math.Max(2, delta.Days);
            return $"{days} days ago";
        }

        return string.Empty;
    }
}
