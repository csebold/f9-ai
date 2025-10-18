using System;
using System.Collections.Generic;

namespace ChatClient.Models;

/// <summary>
/// Represents a model entry returned from a provider catalog, including availability metadata.
/// </summary>
public sealed record ModelCatalogEntry(
    string Id,
    bool IsInstalled,
    bool IsDownloadable,
    bool IsRecommended)
{
    public string DisplayName
    {
        get
        {
            var tags = new List<string>();

            if (IsInstalled)
            {
                tags.Add("installed");
            }

            if (!IsInstalled && IsDownloadable)
            {
                tags.Add("download");
            }
            else if (IsDownloadable && IsInstalled)
            {
                tags.Add("update");
            }

            if (IsRecommended)
            {
                tags.Add("recommended");
            }

            if (tags.Count == 0)
            {
                return Id;
            }

            return $"{Id} ({string.Join(", ", tags)})";
        }
    }

    public ModelCatalogEntry Merge(ModelCatalogEntry other)
    {
        if (!string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot merge entries with different identifiers.");
        }

        return new ModelCatalogEntry(
            Id,
            IsInstalled || other.IsInstalled,
            IsDownloadable || other.IsDownloadable,
            IsRecommended || other.IsRecommended);
    }
}
