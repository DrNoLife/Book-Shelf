namespace LnShelf.Api.Features.Metadata.Mappers;

/// <summary>
/// The dirty→clean edge for MangaUpdates (CLAUDE.md — Mapping).
/// </summary>
public static class MangaUpdatesSeriesMapper
{
    /// <summary>
    /// MangaUpdates suffixes every novel's title with " (Novel)". Left in place it
    /// costs a token on every score, so it comes off at the boundary.
    /// </summary>
    private const string NovelSuffix = " (Novel)";

    public static ReleaseSeriesCandidate ToCandidate(MangaUpdatesSeriesRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new ReleaseSeriesCandidate
        {
            ExternalId = record.SeriesId,
            Title = StripNovelSuffix(record.Title),
            SourceType = record.Type,
            StartYear = ToYear(record.Year),
        };
    }

    public static ReleaseSeriesInfo ToSeriesInfo(MangaUpdatesSeriesRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new ReleaseSeriesInfo
        {
            ExternalId = record.SeriesId,
            Title = StripNovelSuffix(record.Title),
            AssociatedTitles = ToAssociatedTitles(record.Associated),
            SourceType = record.Type,
            StartYear = ToYear(record.Year),
            IsCompleted = record.Completed,
            VolumeCount = MangaUpdatesVolumeCountParser.Parse(record.Status),
            RawStatus = NullIfBlank(record.Status),
        };
    }

    /// <summary>
    /// The alternative titles, suffix stripped. Kept in full — including the
    /// Arabic, Russian and Thai ones — because the scorer decides what is close,
    /// not the mapper.
    /// </summary>
    private static string[] ToAssociatedTitles(List<MangaUpdatesAssociatedTitle>? associated)
    {
        if (associated is null)
        {
            return [];
        }

        var titles = new List<string>(associated.Count);
        foreach (var entry in associated)
        {
            var title = StripNovelSuffix(entry.Title);
            if (title.Length > 0)
            {
                titles.Add(title);
            }
        }

        return [.. titles];
    }

    private static string StripNovelSuffix(string? title)
    {
        var trimmed = title?.Trim() ?? string.Empty;

        return trimmed.EndsWith(NovelSuffix, StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^NovelSuffix.Length].TrimEnd()
            : trimmed;
    }

    /// <summary>The year arrives as a string, and sometimes as an empty one.</summary>
    private static int? ToYear(string? year) =>
        int.TryParse(year, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
