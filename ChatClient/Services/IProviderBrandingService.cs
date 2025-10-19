using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ChatClient.Services;

/// <summary>
/// Provides cached branding metadata for LLM providers, including favicon paths.
/// </summary>
public interface IProviderBrandingService
{
    /// <summary>
    /// Gets branding for the specified provider, refreshing the cached icon if necessary.
    /// </summary>
    Task<ProviderBranding> GetBrandingAsync(LlmProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes all provider branding assets and returns the updated cache snapshot.
    /// </summary>
    Task<IReadOnlyDictionary<LlmProvider, ProviderBranding>> RefreshAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes branding for the specified provider and returns the updated data.
    /// </summary>
    Task<ProviderBranding> RefreshAsync(LlmProvider provider, CancellationToken cancellationToken = default);
}

public sealed record ProviderBranding(LlmProvider Provider, string DisplayName, string? IconPath);
