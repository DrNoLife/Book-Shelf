namespace LnShelf.Api.Features.Shelf.Services;

/// <summary>
/// Links a series being added from AniList to its release source, and sizes its volumes by
/// the release source's count rather than AniList's.
/// </summary>
// AniList counts the Japanese original and folds bonus volumes into the total: it says ten
// for Torture Princess, whose English edition is nine volumes plus the bonus Vol. 7.5. The
// volumes were created from that ten, and since sync never deletes a volume, the shelf kept
// a volume 10 that no store or library has ever heard of. Asking the release source before
// the volumes exist means the phantom is never created, rather than created and cleaned up.
//
// Best effort. If the release source cannot be reached or finds no match, the AniList volumes
// stand, exactly as before, and the first refresh tries the link again.
public sealed class AddTimeReleaseLink(
    IReleaseSourceMatcher releaseSourceMatcher,
    LnShelfDbContext dbContext,
    ILogger<AddTimeReleaseLink> logger)
{
    public async Task ApplyAsync(Series series, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);

        // Every link already on this shelf, so the new series cannot be given a record that
        // another series holds — the same rule the refresh applies.
        var linkedExternalIds = (await dbContext.Series
                .Where(candidate => candidate.MangaUpdatesId != null)
                .Select(candidate => candidate.MangaUpdatesId!.Value)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        ReleaseSourceMatch match;

        try
        {
            match = await releaseSourceMatcher.MatchAsync(series, linkedExternalIds, cancellationToken);
        }
        // Every failure but a cancelled request, not only the outages a search would fall
        // back on: a 429, a full rate-limit queue or an unreadable record must not turn an add
        // that AniList alone could complete into an error. The first refresh asks again.
        catch (Exception exception) when (exception is not OperationCanceledException
                                          || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "Release source lookup failed while adding {TitleEnglish}; keeping AniList's {VolumeCount} volumes",
                series.TitleEnglish,
                series.Volumes.Count);

            KeepAniListCount(series);
            return;
        }

        if (!match.IsLinked)
        {
            KeepAniListCount(series);
            return;
        }

        series.MangaUpdatesId = match.MatchedExternalId;

        if (match.MatchedSeries?.VolumeCount is not { } volumeCount)
        {
            KeepAniListCount(series);
            return;
        }

        logger.LogInformation(
            "Adding {TitleEnglish} with {VolumeCount} volumes from the release source (AniList said {AniListVolumeCount})",
            series.TitleEnglish,
            volumeCount,
            series.Volumes.Count);

        // Replaced outright: these volumes were made a moment ago from AniList's count, are not
        // saved yet and carry nothing of the reader's.
        series.Volumes.Clear();

        for (var number = 1; number <= volumeCount; number++)
        {
            series.Volumes.Add(SeriesMetadataMapper.ToVolume(new VolumeMetadata { Number = number }));
        }
    }

    /// <summary>
    /// Records that the volumes stand on AniList's count, because the release source gave none.
    /// </summary>
    // Only on the paths where MangaUpdates said nothing about the count. When it did, the
    // refresh that follows the add writes the count and its provenance together
    // (ReleaseCountWriter), which is the one place a release-source basis is written.
    private static void KeepAniListCount(Series series)
    {
        series.VolumeCountBasis = VolumeCountBasis.AniList;
        series.VolumeCountEvidence = null;
    }
}
