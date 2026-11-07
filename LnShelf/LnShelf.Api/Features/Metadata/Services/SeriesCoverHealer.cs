namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Gives a series its own cover when it has none — the series whose cover could not be
/// cached when it was added or imported, because the store or the CDN hiccuped then.
/// </summary>
// Without this a failed cache was final: the series cover is only asked for at add and
// import time, and CoverFetcher answers a failure with null rather than an error, so the
// series showed a placeholder for good (audit 2026-10-09 #16).
//
// Asks the source the series came from, in the same order the add flows use: AniList when
// the series has an AniList id, MangaUpdates only for a series that has nothing else (one
// added through the MangaUpdates fallback). The key is the source's own, so it is the key
// the add would have written.
//
// The series key only. A user's cover is a volume override (VolumeUserState), which this
// neither reads nor writes; every projection puts an override above the series cover, so
// healing beneath one changes nothing the reader chose (CLAUDE.md — Cover overrides).
//
// No budget, for SeriesDescriptionHealer's reason: self-extinguishing the moment a key is
// written. Unlike the description there is no "asked, and there is none" state to write
// down, so a series whose source carries no cover is asked again each night — one request
// per such series, which on a shelf this size is not worth a column to save.
public sealed class SeriesCoverHealer(
    IMangaUpdatesSeriesQueryService mangaUpdatesSource,
    ISeriesCoverCache coverCache,
    ILogger<SeriesCoverHealer> logger)
{
    /// <param name="aniList">
    /// The series' AniList record, shared with the description heal; null when the series has
    /// no AniList id.
    /// </param>
    public async Task HealAsync(Series series, SeriesMetadataLookup? aniList, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (series.CoverObjectKey is not null)
        {
            return;
        }

        try
        {
            var objectKey = (aniList, series.MangaUpdatesId) switch
            {
                ({ } lookup, _) => await FromAniListAsync(lookup, cancellationToken),
                (null, { } mangaUpdatesId) => await FromMangaUpdatesAsync(mangaUpdatesId, cancellationToken),
                _ => null,
            };

            // Null is a failed or empty answer, and the column stays null so the next run asks
            // again: a failed fetch is not evidence that there is no cover.
            if (objectKey is null)
            {
                return;
            }

            series.CoverObjectKey = objectKey;

            logger.LogInformation(
                "{TitleEnglish}: series cover cached again as {ObjectKey}",
                series.TitleEnglish,
                objectKey);
        }
        // The caller's cancellation only: a timed-out request is a cancelled task nobody
        // asked for, and it is exactly the failure this catch exists to absorb.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A cover is decoration: a refresh that got the volumes right is not worth failing
            // because the source could not be asked for one. The guard is still true tomorrow.
            logger.LogWarning(
                exception,
                "Could not fetch a series cover for {TitleEnglish}; continuing",
                series.TitleEnglish);
        }
    }

    private async Task<string?> FromAniListAsync(SeriesMetadataLookup aniList, CancellationToken cancellationToken)
    {
        var metadata = await aniList.GetAsync(cancellationToken);

        return await coverCache.EnsureCachedAsync(
            MetadataSourceName.AniList, aniList.AniListId, metadata?.CoverImageUrl, cancellationToken);
    }

    private async Task<string?> FromMangaUpdatesAsync(long mangaUpdatesId, CancellationToken cancellationToken)
    {
        var metadata = await mangaUpdatesSource.GetSeriesAsync(mangaUpdatesId, cancellationToken);

        return await coverCache.EnsureCachedAsync(
            MetadataSourceName.MangaUpdates, mangaUpdatesId, metadata?.CoverImageUrl, cancellationToken);
    }
}
