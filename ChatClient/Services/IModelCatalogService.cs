using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ChatClient.Models;

namespace ChatClient.Services;

public interface IModelCatalogService
{
    Task<IReadOnlyList<string>> GetModelsAsync(LlmProvider provider, ProviderSettings providerSettings, CancellationToken cancellationToken);
}
