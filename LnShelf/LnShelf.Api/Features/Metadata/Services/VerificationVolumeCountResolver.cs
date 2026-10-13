namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// How many volumes a verification run should check, and on whose authority.
/// </summary>
// The precedence order is the shelf's own (CLAUDE.md gives volume counts to MangaUpdates),
// with one addition for the case neither series-level source can answer: the store's own
// highest released volume number. That ground is weaker and is named as such in the report
// rather than quietly used.
public sealed class VerificationVolumeCountResolver(
    IReleaseSourceMatcher releaseSourceMatcher,
    IVolumeCatalogueSource catalogueSource,
    CatalogueVolumeMatcher catalogueMatcher,
    TimeProvider timeProvider)
{
    public async Task<VerificationVolumeCount> ResolveAsync(
        Series series,
        ResolvedSeries resolved,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(resolved);

        // The release source itself answered the search, so its count is already in hand
        // and no title matching is needed to earn it — the id came from that catalogue.
        if (resolved.ReleaseVolumeCount is { } resolvedCount)
        {
            return new VerificationVolumeCount(
                resolvedCount, $"{resolvedCount} from the release source", FromReleaseSource: true);
        }

        var match = await releaseSourceMatcher.MatchAsync(series, new HashSet<long>(), cancellationToken);

        if (match.IsLinked && match.MatchedSeries?.VolumeCount is { } releaseCount)
        {
            return new VerificationVolumeCount(
                releaseCount, $"{releaseCount} from the release source", FromReleaseSource: true);
        }

        // Non-fatal by design: an unlinked series still has volumes, and the point of the
        // run is what the per-volume sources can say about them.
        var note = match.IsLinked
            ? $"linked, but stated no volume count ({match.Explanation})"
            : $"unlinked ({match.Outcome}: {match.Explanation})";

        if (resolved.ListedVolumeCount > 0)
        {
            return new VerificationVolumeCount(
                resolved.ListedVolumeCount,
                $"{resolved.ListedVolumeCount} from the metadata source",
                FromReleaseSource: false)
            {
                ReleaseSourceNote = note,
            };
        }

        var catalogueCount = await HighestReleasedCatalogueNumberAsync(series, cancellationToken);

        return new VerificationVolumeCount(
            catalogueCount,
            catalogueCount is 0
                ? "none (no source named a volume count)"
                : $"{catalogueCount} from the store catalogue's highest released volume",
            FromReleaseSource: false)
        {
            ReleaseSourceNote = note,
        };
    }

    /// <summary>
    /// The highest volume number the store catalogue positively confirms for this series and
    /// dates on or before today.
    /// </summary>
    // Released only, because the store lists pre-orders: checking a volume that is not out yet
    // would report every per-volume source as missing it.
    //
    // The same two filters the enrichment pass applies, through the same matcher, for the
    // same reason: a store search answers with spin-offs, the other medium and unrelated
    // series that share a word. Cheap, because the catalogue source caches its answer for a
    // day — the enrichment pass that follows reads the same response rather than asking
    // again.
    private async Task<int> HighestReleasedCatalogueNumberAsync(
        Series series,
        CancellationToken cancellationToken)
    {
        var catalogue = await catalogueSource.GetVolumesAsync(
            series.TitleEnglish, series.Format, cancellationToken);

        return catalogueMatcher.Match(series, series.TitleEnglish, catalogue)
            .HighestReleasedNumber(timeProvider.Today());
    }
}

/// <summary>How many volumes are being checked, and who said so.</summary>
public sealed record VerificationVolumeCount(int Volumes, string Ground, bool FromReleaseSource)
{
    /// <summary>Set when the release source did not supply the count. Non-fatal.</summary>
    public string? ReleaseSourceNote { get; init; }
}
