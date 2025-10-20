using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Models;

namespace ChatClient.Services;

public interface IChatFileService
{
    IReadOnlyList<ProjectFile> GetFiles(ProjectSettings project, string sessionId);

    Task<ProjectFile> AddFileAsync(ProjectSettings project, string sessionId, string sourceFilePath, CancellationToken cancellationToken = default);

    Task DeleteFileAsync(ProjectSettings project, string sessionId, string fileName, CancellationToken cancellationToken = default);
}
