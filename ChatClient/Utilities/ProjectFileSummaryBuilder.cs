using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ChatClient.Utilities;

public static class ProjectFileSummaryBuilder
{
    private const int DefaultMaxFiles = 25;

    private static readonly HashSet<string> DirectoryExclusions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".svn",
        ".hg",
        ".idea",
        ".vs",
        ".vscode",
        "node_modules",
        "bin",
        "obj",
        "artifacts",
        "packages",
        ".tox",
        ".pytest_cache",
        "__pycache__",
        "dist",
        "build"
    };

    public static string Build(string? workspacePath, int maxFiles = DefaultMaxFiles)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return string.Empty;
        }

        if (!Directory.Exists(workspacePath))
        {
            return string.Empty;
        }

        try
        {
            var listedFiles = new List<string>(Math.Min(maxFiles, DefaultMaxFiles));
            var totalFiles = EnumerateFiles(workspacePath, maxFiles, listedFiles);
            if (totalFiles == 0 || listedFiles.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();

            builder.Append("Project files available (")
                   .Append(totalFiles.ToString(CultureInfo.InvariantCulture))
                   .Append(totalFiles == 1 ? " file" : " files")
                   .Append("). Reference them by relative path when you need their contents. ");

            if (totalFiles <= listedFiles.Count)
            {
                builder.Append("Listing all files:");
            }
            else
            {
                builder.Append("Listing first ")
                       .Append(listedFiles.Count.ToString(CultureInfo.InvariantCulture))
                       .Append(listedFiles.Count == 1 ? " file:" : " files:");
            }

            builder.AppendLine();

            foreach (var file in listedFiles)
            {
                builder.Append("- ")
                       .Append(file)
                       .AppendLine();
            }

            if (totalFiles > listedFiles.Count)
            {
                var remaining = totalFiles - listedFiles.Count;
                builder.Append("+ ")
                       .Append(remaining.ToString(CultureInfo.InvariantCulture))
                       .Append(remaining == 1 ? " more file not listed." : " more files not listed.");
            }

            return builder.ToString().Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static int EnumerateFiles(string root, int maxFiles, List<string> listedFiles)
    {
        var totalFiles = 0;
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            IEnumerable<string>? directories = null;
            try
            {
                directories = Directory.EnumerateDirectories(current);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

            if (directories is not null)
            {
                var orderedDirectories = directories
                    .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                for (var index = orderedDirectories.Count - 1; index >= 0; index--)
                {
                    var directory = orderedDirectories[index];
                    var name = Path.GetFileName(directory);
                    if (ShouldSkipDirectory(directory, name))
                    {
                        continue;
                    }

                    pending.Push(directory);
                }
            }

            IEnumerable<string>? files = null;
            try
            {
                files = Directory.EnumerateFiles(current);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

            if (files is null)
            {
                continue;
            }

            foreach (var file in files)
            {
                if (ShouldSkipFile(file))
                {
                    continue;
                }

                totalFiles++;
                if (listedFiles.Count < maxFiles)
                {
                    var relative = Path.GetRelativePath(root, file);
                    var normalized = relative.Replace(Path.DirectorySeparatorChar, '/');
                    listedFiles.Add(normalized);
                }
            }
        }

        listedFiles.Sort(StringComparer.OrdinalIgnoreCase);
        return totalFiles;
    }

    private static bool ShouldSkipDirectory(string? fullPath, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (DirectoryExclusions.Contains(name))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return false;
        }

        try
        {
            var attributes = File.GetAttributes(fullPath);
            if ((attributes & FileAttributes.Hidden) != 0 || (attributes & FileAttributes.System) != 0)
            {
                return true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return false;
    }

    private static bool ShouldSkipFile(string file)
    {
        try
        {
            var attributes = File.GetAttributes(file);
            if ((attributes & FileAttributes.Hidden) != 0 || (attributes & FileAttributes.System) != 0)
            {
                return true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }

        return false;
    }
}
