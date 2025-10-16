using System;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Models;

namespace ChatClient.Services;

public interface IStartupInitializer
{
    Task<StartupInitializationResult> InitializeAsync(
        IProgress<string> statusProgress,
        CancellationToken cancellationToken);
}
