using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Models;

namespace ChatClient.Services;

public interface IProjectFileService
{
    IReadOnlyList<ProjectFile> GetFiles(ProjectSettings project);

    Task<ProjectFile> AddFileAsync(ProjectSettings project, string sourceFilePath, CancellationToken cancellationToken = default);

    Task DeleteFileAsync(ProjectSettings project, string fileName, CancellationToken cancellationToken = default);
}
