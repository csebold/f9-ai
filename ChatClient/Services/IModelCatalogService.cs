using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ChatClient.Services;

public interface IModelCatalogService
{
    Task<IReadOnlyList<string>> GetModelsAsync(LlmProvider provider, string apiKey, CancellationToken cancellationToken);
}
