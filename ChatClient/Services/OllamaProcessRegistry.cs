using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ChatClient.Services;

internal static class OllamaProcessRegistry
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static void AddOrUpdate(int processId, string logPath)
    {
        if (processId <= 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(logPath))
        {
            logPath = string.Empty;
        }

        lock (Gate)
        {
            var map = LoadInternal();
            map[processId] = new RegistryEntry(processId, logPath, DateTimeOffset.UtcNow);
            SaveInternal(map);
        }
    }

    public static void Remove(int processId)
    {
        if (processId <= 0)
        {
            return;
        }

        lock (Gate)
        {
            var map = LoadInternal();
            if (map.Remove(processId))
            {
                SaveInternal(map);
            }
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            var path = AppPaths.GetOllamaProcessRegistryPath();
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch
                {
                    // Ignore IO errors during cleanup.
                }
            }
        }
    }

    private static Dictionary<int, RegistryEntry> LoadInternal()
    {
        var path = AppPaths.GetOllamaProcessRegistryPath();

        if (!File.Exists(path))
        {
            return new Dictionary<int, RegistryEntry>();
        }

        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<int, RegistryEntry>();
            }

            return JsonSerializer.Deserialize<Dictionary<int, RegistryEntry>>(json, SerializerOptions)
                   ?? new Dictionary<int, RegistryEntry>();
        }
        catch
        {
            return new Dictionary<int, RegistryEntry>();
        }
    }

    private static void SaveInternal(Dictionary<int, RegistryEntry> map)
    {
        var path = AppPaths.GetOllamaProcessRegistryPath();

        if (map.Count == 0)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Ignore failures deleting the file.
            }

            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(map, SerializerOptions);
            File.WriteAllText(path, json);
        }
        catch
        {
            // Ignore write failures.
        }
    }

    private sealed record RegistryEntry(int ProcessId, string LogPath, DateTimeOffset RecordedAt);
}
