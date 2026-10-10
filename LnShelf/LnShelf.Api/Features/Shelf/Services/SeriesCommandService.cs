namespace LnShelf.Api.Features.Shelf.Services;

/// <inheritdoc />
public sealed class SeriesCommandService(
    LnShelfDbContext dbContext,
    AddSeriesPipeline addSeriesPipeline,
    ICoverStore coverStore,
    TimeProvider timeProvider,
    ILogger<SeriesCommandService> logger) : ISeriesCommandService
{
    /// <inheritdoc />
    // The whole of adding a series lives in its own collaborator: it is the only job here
    // that talks to a metadata source, and the only one that is source-specific at all.
    public Task<AddSeriesResponse?> AddAsync(
        AddSeriesRequest request,
        CancellationToken cancellationToken) =>
        addSeriesPipeline.RunAsync(request, cancellationToken);

    public async Task<SeriesDetailResponse?> SetVolumeViewAsync(
        Guid seriesId,
        SetSeriesVolumeViewRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var detail = await WriteUserStateAsync(
            seriesId,
            state => state.VolumeViewOverride = request.View,
            cancellationToken);

        if (detail is not null)
        {
            logger.LogInformation(
                "Series {SeriesId} volume view override is now {VolumeViewOverride}",
                seriesId,
                request.View?.ToString() ?? "the global preference");
        }

        return detail;
    }

    public async Task<SeriesDetailResponse?> SetStateAsync(
        Guid seriesId,
        UpdateSeriesStateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Notes is { Length: > SeriesUserState.NotesMaximumLength })
        {
            throw new InvalidInputException(
                $"A note can be at most {SeriesUserState.NotesMaximumLength} characters; this one is {request.Notes.Length}.");
        }

        var detail = await WriteUserStateAsync(
            seriesId,
            state =>
            {
                // Partial update, on the same rule PreferencesService follows: every null
                // leaves that field alone, so saving a note does not have to send the
                // tracking status back just to avoid clobbering it. An empty Notes is
                // therefore how a note is cleared — a blank one reads as absent on the way
                // out (SeriesDetailProjection), so the two agree.
                state.TrackingStatus = request.TrackingStatus ?? state.TrackingStatus;
                state.Notes = request.Notes ?? state.Notes;
            },
            cancellationToken);

        if (detail is not null)
        {
            logger.LogInformation(
                "Series {SeriesId} is now {TrackingStatus}{NoteChange}",
                seriesId,
                detail.TrackingStatus,
                request.Notes is null ? string.Empty : " with a note");
        }

        return detail;
    }

    public async Task<bool> MarkSeenAsync(Guid seriesId, CancellationToken cancellationToken)
    {
        var state = await dbContext.SeriesUserStates
            .FirstOrDefaultAsync(candidate => candidate.SeriesId == seriesId, cancellationToken);

        if (state is null)
        {
            // No user-state row means nothing has ever been recorded about this series, so
            // the flag it would carry is already false and there is nothing to clear. All
            // that is left to answer is whether the series exists at all.
            return await dbContext.Series
                .AnyAsync(candidate => candidate.Id == seriesId, cancellationToken);
        }

        if (!state.HasNewVolume)
        {
            // Already acknowledged, and this is the common case: the page calls this on
            // every visit, and most visits are to a series with no new volume waiting.
            return true;
        }

        state.HasNewVolume = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Series {SeriesId} new-volume badge acknowledged", seriesId);

        return true;
    }

    /// <summary>
    /// Applies one change to a series' user state and answers with the refreshed series.
    /// </summary>
    // Loaded with its volumes because the answer is the whole detail response: the page
    // then redraws from one round trip, as the bulk endpoint already does.
    /// <returns>Null when no series has that id.</returns>
    private async Task<SeriesDetailResponse?> WriteUserStateAsync(
        Guid seriesId,
        Action<SeriesUserState> change,
        CancellationToken cancellationToken)
    {
        var series = await dbContext.Series
            .Include(candidate => candidate.UserState)
            .Include(candidate => candidate.SourceChoices)
            .Include(candidate => candidate.Volumes).ThenInclude(volume => volume.UserState)
            .FirstOrDefaultAsync(candidate => candidate.Id == seriesId, cancellationToken);

        if (series is null)
        {
            return null;
        }

        // A series with no user-state row has never been touched; any of these writes is a
        // touch, and it is the same row every other per-series preference lives on.
        change(series.UserState ??= SeriesUserState.Untouched(series.Id));

        await dbContext.SaveChangesAsync(cancellationToken);

        return SeriesDetailProjection.ToDetail(
            series, timeProvider.Today());
    }

    public async Task<bool> DeleteAsync(Guid seriesId, CancellationToken cancellationToken)
    {
        var series = await dbContext.Series
            .Include(candidate => candidate.Volumes).ThenInclude(volume => volume.UserState)
            .FirstOrDefaultAsync(candidate => candidate.Id == seriesId, cancellationToken);

        if (series is null)
        {
            return false;
        }

        // Read before the delete, because the rows that name the covers go with the series.
        // Only per-volume keys: a source's series art (anilist/…) is shared by every copy of
        // the series, the same rule AccountDeletionService keeps.
        var volumeCovers = series.Volumes
            .SelectMany(volume => new[] { volume.UserState?.CoverOverrideObjectKey, volume.CoverObjectKey })
            .OfType<string>()
            .Where(key => CoverObjectKey.VolumeIdOf(key) is not null)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Counted before the delete, because afterwards there is nothing to count and
        // the log line is the only record that this shelf ever held them.
        var volumeCount = series.Volumes.Count;

        // RelatedSeriesId is a plain column with no foreign key, so nothing in the
        // database would stop it pointing at a row that no longer exists. Cleared here
        // rather than tolerated: the LN ↔ manga link reads as "tracked separately", and a
        // link to a series that is gone is not that.
        var linkedBack = await dbContext.Series
            .Where(candidate => candidate.RelatedSeriesId == seriesId)
            .ToListAsync(cancellationToken);

        foreach (var candidate in linkedBack)
        {
            candidate.RelatedSeriesId = null;
        }

        // Volumes, both user-state tables and the series' own row go with it, by the
        // cascades configured in SeriesConfiguration and VolumeConfiguration.
        dbContext.Series.Remove(series);
        await dbContext.SaveChangesAsync(cancellationToken);

        // A per-volume key carries the volume's id, and a re-added series gets new volume ids,
        // so once the rows are gone nothing can name these objects again. For the covers the
        // user uploaded that matters most (GDPR Art. 5(1)(e), 17): a kept object would be out
        // of reach of the user and of account deletion alike. The series art stays, and a
        // re-added copy finds it already local.
        await DeleteVolumeCoversAsync(volumeCovers, cancellationToken);

        logger.LogInformation(
            "Deleted {TitleEnglish} ({SeriesId}) with {VolumeCount} volumes and all user state on them; "
            + "{LinkedBackCount} relation(s) cleared",
            series.TitleEnglish,
            seriesId,
            volumeCount,
            linkedBack.Count);

        return true;
    }

    /// <summary>Removes the per-volume covers the shelf no longer points at.</summary>
    // Never fatal, for the reason VolumeCommandService gives: the delete is already saved,
    // and an upload that could not be removed is picked up by the cover-orphan-sweep.
    private async Task DeleteVolumeCoversAsync(IReadOnlyList<string> objectKeys, CancellationToken cancellationToken)
    {
        foreach (var objectKey in objectKeys)
        {
            try
            {
                await coverStore.DeleteAsync(objectKey, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Could not delete the cover {ObjectKey} of a deleted series; cover-orphan-sweep removes it if it is an upload",
                    objectKey);
            }
        }
    }
}
