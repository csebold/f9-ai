using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Models;

namespace ChatClient.Services;

public interface IModelCatalogService
{
    Task<IReadOnlyList<ModelCatalogEntry>> GetModelsAsync(
        LlmProvider provider,
        ProviderSettings providerSettings,
        CancellationToken cancellationToken);

    Task DownloadModelAsync(
        LlmProvider provider,
        ProviderSettings providerSettings,
        string modelId,
        CancellationToken cancellationToken);
}
