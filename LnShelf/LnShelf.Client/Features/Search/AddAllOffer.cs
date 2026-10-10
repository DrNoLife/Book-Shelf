namespace LnShelf.Client.Features.Search;

/// <summary>
/// "Add all 5 parts (33 vols)": the main story's one-press add, offered only when a single
/// format is chosen and at least two of its entries are not on the shelf yet.
/// </summary>
public sealed record AddAllOffer
{
    /// <summary>The entries the press adds, in reading order.</summary>
    public required IReadOnlyList<SearchWorkEntry> Entries { get; init; }

    /// <summary>"Add all 5 parts (33 vols)".</summary>
    public required string Label { get; init; }

    /// <summary>"Add all 5": the phone's narrower row.</summary>
    public required string ShortLabel { get; init; }

    /// <returns>Null when there is nothing worth a one-press add.</returns>
    // Only under one format: under All the main story mixes the light novel's parts with the
    // manga's, and "add all" would put both on the shelf when the reader wanted one of them.
    public static AddAllOffer? For(SearchWork work, Format? formatFilter, Func<SeriesSearchResultResponse, bool> isOnShelf)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(isOnShelf);

        if (formatFilter is null)
        {
            return null;
        }

        var missing = work.MainStory.Where(entry => !isOnShelf(entry.Result)).ToList();
        if (missing.Count < 2)
        {
            return null;
        }

        var noun = work.MainStory.Any(entry => entry.PartNumber is not null) ? "parts" : "series";
        var lead = missing.Count == work.MainStory.Count ? "Add all" : "Add the other";

        return new AddAllOffer
        {
            Entries = missing,
            Label = $"{lead} {missing.Count} {noun}{VolumeTotal(missing)}",
            ShortLabel = $"{lead} {missing.Count}",
        };
    }

    /// <summary>" (33 vols)", or " (21+ vols)" when some counts are unknown; empty when none is known.</summary>
    private static string VolumeTotal(List<SearchWorkEntry> entries)
    {
        var known = entries.Sum(entry => entry.Result.VolumeCount ?? 0);
        var anyUnknown = entries.Any(entry => entry.Result.VolumeCount is null);

        return known == 0 ? string.Empty : $" ({known}{(anyUnknown ? "+" : string.Empty)} vols)";
    }
}
