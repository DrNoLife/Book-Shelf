namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Forgets a volume's synced cover key when the store no longer holds the file it names, so
/// the enrichment that follows fetches the art again.
/// </summary>
// Every cover rule downstream trusts the column: VolumeGaps counts a volume with a key as
// covered, and VolumeFactWriter caches fill-if-null on it. So a key whose file is gone — a
// cover directory restored without its files, a volume mount swapped, a file deleted by hand
// — left the volume coverless for good: no pass ever asked about it again, the cover endpoint
// answered 404 and the tile fell back to the series art. Observed on 2026-10-13, 153 volumes
// at once, while the sync logged a clean match for every one of them.
//
// Cleared rather than re-fetched here, so the rules that decide whether and what to fetch
// (the placeholder screen, the precedence between sources) stay in one place and the healed
// volume goes through them like any other gap.
//
// Only the synced key, never VolumeUserState.CoverOverrideObjectKey: an override is user
// data, and sync does not write user state even to tidy it (CLAUDE.md).
//
// One existence check per covered volume per run. The store is a local directory today
// (FileSystemCoverStore), so that is a stat call, not a request.
public sealed class DanglingVolumeCoverHealer(
    ICoverStore coverStore,
    ILogger<DanglingVolumeCoverHealer> logger)
{
    public async Task HealAsync(Series series, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);

        foreach (var volume in series.Volumes)
        {
            if (volume.CoverObjectKey is not { } objectKey
                || await coverStore.ExistsAsync(objectKey, cancellationToken))
            {
                continue;
            }

            logger.LogWarning(
                "{TitleEnglish} volume {VolumeNumber}: the cover store has no {ObjectKey}; "
                + "forgetting it so the art is fetched again",
                series.TitleEnglish,
                volume.Number,
                objectKey);

            volume.CoverObjectKey = null;
        }
    }
}
