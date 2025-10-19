using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace ChatClient.Converters;

/// <summary>
/// Converts an absolute file path into a cached <see cref="Bitmap"/> instance for binding scenarios.
/// </summary>
public sealed class FilePathToBitmapConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, WeakReference<Bitmap>> Cache = new(StringComparer.Ordinal);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
        {
            return AvaloniaProperty.UnsetValue;
        }

        if (!File.Exists(path))
        {
            return AvaloniaProperty.UnsetValue;
        }

        if (Cache.TryGetValue(path, out var reference) &&
            reference.TryGetTarget(out var cached))
        {
            return cached;
        }

        try
        {
            var bitmap = new Bitmap(path);
            Cache[path] = new WeakReference<Bitmap>(bitmap);
            return bitmap;
        }
        catch
        {
            return AvaloniaProperty.UnsetValue;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("FilePathToBitmapConverter does not support ConvertBack.");
}
