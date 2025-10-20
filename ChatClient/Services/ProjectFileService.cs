using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Models;

namespace ChatClient.Services;

public sealed class ProjectFileService : IProjectFileService
{
    public IReadOnlyList<ProjectFile> GetFiles(ProjectSettings project)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        var directory = ProjectWorkspace.GetProjectFilesDirectory(project);

        try
        {
            var files = Directory.EnumerateFiles(directory)
                .Select(path =>
                {
                    try
                    {
                        var info = new FileInfo(path);
                        return info.Exists
                            ? new ProjectFile(info.Name, info.FullName, info.Length, info.LastWriteTimeUtc)
                            : null;
                    }
                    catch (IOException)
                    {
                        return null;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        return null;
                    }
                })
                .Where(file => file is not null)
                .Select(file => file!)
                .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return files;
        }
        catch (IOException)
        {
            return Array.Empty<ProjectFile>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<ProjectFile>();
        }
    }

    public async Task<ProjectFile> AddFileAsync(ProjectSettings project, string sourceFilePath, CancellationToken cancellationToken = default)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            throw new ArgumentException("Source file path must be provided.", nameof(sourceFilePath));
        }

        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("File to upload was not found.", sourceFilePath);
        }

        var directory = ProjectWorkspace.GetProjectFilesDirectory(project);
        var fileName = Path.GetFileName(sourceFilePath);
        var targetFileName = EnsureUniqueFileName(directory, fileName);
        var targetPath = Path.Combine(directory, targetFileName);

        await using var sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var destinationStream = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await sourceStream.CopyToAsync(destinationStream, 81920, cancellationToken).ConfigureAwait(false);

        var info = new FileInfo(targetPath);
        return new ProjectFile(info.Name, info.FullName, info.Length, info.LastWriteTimeUtc);
    }

    public Task DeleteFileAsync(ProjectSettings project, string fileName, CancellationToken cancellationToken = default)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name must be provided.", nameof(fileName));
        }

        var directory = ProjectWorkspace.GetProjectFilesDirectory(project);
        var targetPath = Path.Combine(directory, fileName);

        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }

        return Task.CompletedTask;
    }

    private static string EnsureUniqueFileName(string directory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "file";
        }

        var sanitized = fileName.Trim();
        var name = Path.GetFileNameWithoutExtension(sanitized);
        var extension = Path.GetExtension(sanitized);

        if (string.IsNullOrEmpty(name))
        {
            name = "file";
        }

        var targetPath = Path.Combine(directory, sanitized);
        if (!File.Exists(targetPath))
        {
            return sanitized;
        }

        var index = 1;
        while (true)
        {
            var candidateName = string.Format(CultureInfo.InvariantCulture, "{0} ({1}){2}", name, index, extension);
            var candidatePath = Path.Combine(directory, candidateName);
            if (!File.Exists(candidatePath))
            {
                return candidateName;
            }

            index++;
        }
    }
}
