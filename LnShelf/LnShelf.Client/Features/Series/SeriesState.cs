namespace LnShelf.Client.Features.Series;

/// <summary>
/// The series-detail page's state: one fetch, then writes that each answer with the
/// whole series again.
/// </summary>
// The HTTP lives in SeriesDetailClient and the menu-and-selection state in
// VolumeSelectionState; what is left here is the series, the flags a page renders from,
// and the one wrapper that keeps every write's ceremony identical.
public sealed class SeriesState(SeriesDetailClient client, SourceConflictState sourceConflicts)
{
    public SeriesDetailResponse? Series { get; private set; }

    /// <summary>
    /// What each source said about every disagreement on this series, settled or not. Loaded
    /// with the series and again after every choice.
    /// </summary>
    public IReadOnlyList<SourceConflictResponse> Conflicts => sourceConflicts.Conflicts;

    public bool IsLoading { get; private set; }
    public bool LoadFailed { get; private set; }

    /// <summary>True while a write is in flight — the tiles go inert.</summary>
    public bool IsSaving { get; private set; }

    public string? Message { get; private set; }

    /// <summary>
    /// Incremented on every cover write. The override object key never changes, so
    /// this is what tells the browser the bytes behind it did.
    /// </summary>
    public int CoverVersion { get; private set; }

    public event Action? Changed;

    public async Task LoadAsync(Guid seriesId, CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        LoadFailed = false;
        Message = null;
        Changed?.Invoke();

        try
        {
            var loaded = await client.GetAsync(seriesId, cancellationToken);

            // A response that lands after its load was superseded is dropped whole; the
            // newer load owns the flags now, so none of them is touched on the way out.
            cancellationToken.ThrowIfCancellationRequested();
            Series = loaded;

            if (loaded is null)
            {
                sourceConflicts.Clear();
            }
            else
            {
                await sourceConflicts.LoadAsync(loaded.SeriesId, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (HttpRequestException)
        {
            // Cleared, not kept: after a delete the route's series is gone, and a hero for
            // it drawn over "could not load" would be the stale copy this load replaced.
            Series = null;
            sourceConflicts.Clear();
            LoadFailed = true;
        }

        IsLoading = false;
        Changed?.Invoke();

        // Visiting the series *is* the acknowledgement (CLAUDE.md — HasNewVolume is
        // last-visit state, not a derivation). Awaited rather than dropped, but only after
        // the notification above has already put the series on screen, so the reader never
        // waits on a round trip that is bookkeeping rather than content.
        if (Series is { HasNewVolume: true })
        {
            await MarkSeenAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Unread → Reading → Read → Unread. The cycle lives here rather than on the
    /// server, so the endpoint stays an idempotent "set this status".
    /// </summary>
    public static ReadStatus NextInCycle(ReadStatus current) => current switch
    {
        ReadStatus.Unread => ReadStatus.Reading,
        ReadStatus.Reading => ReadStatus.Read,
        _ => ReadStatus.Unread,
    };

    public Task CycleReadAsync(VolumeResponse volume, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(volume);

        return SetReadAsync(volume, NextInCycle(volume.ReadStatus), cancellationToken);
    }

    public Task ToggleOwnedAsync(VolumeResponse volume, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(volume);

        return SetOwnedAsync(volume, !volume.Owned, format: null, cancellationToken);
    }

    /// <summary>Sets a read status outright — what the menu does, as against the cycle.</summary>
    public Task SetReadAsync(
        VolumeResponse volume,
        ReadStatus status,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(volume);

        return RunAsync(async () =>
            ReplaceSeries(await client.SetReadAsync(volume.VolumeId, status, cancellationToken)));
    }

    /// <summary>
    /// Sets when a read volume was read, or clears the date with null. A date the shelf
    /// would refuse is reported here, before any call, in words.
    /// </summary>
    /// <returns>Whether the date was saved.</returns>
    public Task<bool> SetReadDateAsync(
        VolumeResponse volume,
        DateOnly? readAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(volume);

        if (readAt is { } date && ReadDateInput.ProblemWith(date) is { } problem)
        {
            ReportProblem(problem);
            return Task.FromResult(false);
        }

        return RunAsync(async () =>
            ReplaceSeries(await client.SetReadDateAsync(volume.VolumeId, readAt, cancellationToken)));
    }

    /// <summary>
    /// Sets when these read volumes were read, in one call — the select mode's "Set read
    /// date…". The caller passes only volumes that are read; the API refuses the rest.
    /// </summary>
    /// <returns>Whether the dates were saved — a failed save keeps the panel open.</returns>
    public Task<bool> SetReadDatesAsync(
        IReadOnlyList<VolumeResponse> volumes,
        DateOnly? readAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(volumes);

        if (Series is not { } series || volumes.Count == 0)
        {
            return Task.FromResult(false);
        }

        if (readAt is { } date && ReadDateInput.ProblemWith(date) is { } problem)
        {
            ReportProblem(problem);
            return Task.FromResult(false);
        }

        var volumeIds = volumes.Select(volume => volume.VolumeId).ToList();

        return RunAsync(async () => ReplaceSeries(
            await client.SetReadDatesAsync(series.SeriesId, volumeIds, readAt, cancellationToken)));
    }

    /// <summary>
    /// Sets ownership outright, optionally with the edition. Passing a format on an
    /// owned volume is how the menu switches physical to digital without a second call.
    /// </summary>
    public Task SetOwnedAsync(
        VolumeResponse volume,
        bool owned,
        OwnedFormat? format = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(volume);

        return RunAsync(async () =>
            ReplaceSeries(await client.SetOwnedAsync(volume.VolumeId, owned, format, cancellationToken)));
    }

    /// <summary>"Mark 1–N read" / "Mark 1–N owned".</summary>
    public Task MarkThroughAsync(
        decimal throughNumber,
        ReadStatus? read,
        bool? owned,
        CancellationToken cancellationToken = default) =>
        Series is { } series
            ? RunAsync(async () => ReplaceSeries(await client.MarkThroughAsync(
                series.SeriesId, throughNumber, read, owned, cancellationToken)))
            : Task.CompletedTask;

    /// <summary>
    /// Applies a read status, ownership, or both to exactly these volumes — the select
    /// mode's marks when the selection is not a run from volume 1.
    /// </summary>
    // One single-volume write per volume, in sequence, keeping the last whole-series
    // answer. The bulk endpoint only speaks "1–N", and widening it for a hobby shelf's
    // occasional scattered selection was more API than a handful of sequential PUTs.
    // A refusal part-way stops the loop with the volumes before it written, and the
    // message says so through RunAsync like every other failed write.
    public Task MarkVolumesAsync(
        IReadOnlyList<VolumeResponse> volumes,
        ReadStatus? read,
        bool? owned,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(volumes);

        return RunAsync(async () =>
        {
            foreach (var volume in volumes)
            {
                if (read is { } status)
                {
                    ReplaceSeries(await client.SetReadAsync(volume.VolumeId, status, cancellationToken));
                }

                if (owned is { } isOwned)
                {
                    ReplaceSeries(await client.SetOwnedAsync(
                        volume.VolumeId, isOwned, format: null, cancellationToken));
                }
            }
        });
    }

    /// <summary>
    /// Sets this series' volume view, or clears the override with null so it follows the
    /// global preference again.
    /// </summary>
    public Task SetVolumeViewOverrideAsync(
        VolumeView? view,
        CancellationToken cancellationToken = default) =>
        Series is { } series
            ? RunAsync(async () => ReplaceSeries(await client.SetVolumeViewOverrideAsync(
                series.SeriesId, view, cancellationToken)))
            : Task.CompletedTask;

    /// <summary>Mutes or unmutes notifications about this series. Pushes only; the activity stays.</summary>
    public Task SetNotificationsMutedAsync(bool muted, CancellationToken cancellationToken = default) =>
        Series is { } series
            ? RunAsync(async () => ReplaceSeries(await client.SetNotificationsMutedAsync(
                series.SeriesId, muted, cancellationToken)))
            : Task.CompletedTask;

    /// <summary>How the series is being tracked. The note is left alone.</summary>
    public Task SetTrackingStatusAsync(
        TrackingStatus trackingStatus,
        CancellationToken cancellationToken = default) =>
        SetStateAsync(trackingStatus, notes: null, cancellationToken);

    /// <summary>
    /// The note on the series. An empty string clears it; that is the endpoint's own
    /// rule, because null there means "leave the note alone".
    /// </summary>
    /// <returns>Whether the note was saved — a failed save keeps the editor open.</returns>
    public Task<bool> SetNotesAsync(string notes, CancellationToken cancellationToken = default) =>
        SetStateAsync(trackingStatus: null, notes ?? string.Empty, cancellationToken);

    /// <summary>
    /// Removes the loaded series from the shelf. The caller confirms first.
    /// </summary>
    // No optimistic update and no local removal: the page this state belongs to is about
    // to be navigated away from, so the honest thing is to report whether the server
    // actually did it and let the caller leave.
    public Task<bool> DeleteAsync(CancellationToken cancellationToken = default) =>
        Series is { } series
            ? RunAsync(() => client.DeleteAsync(series.SeriesId, cancellationToken))
            : Task.FromResult(false);

    /// <summary>Uploads a cover override for one volume.</summary>
    public Task SetCoverAsync(
        VolumeResponse volume,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(volume);
        ArgumentNullException.ThrowIfNull(content);

        if (Series is null)
        {
            return Task.CompletedTask;
        }

        return RunAsync(async () =>
        {
            var updated = await client.SetCoverAsync(
                volume.VolumeId, content, fileName, cancellationToken);

            if (updated is not null)
            {
                CoverVersion++;
                ReplaceSeries(updated);
                Message = $"Cover set for volume {volume.Number.ToVolumeText()}.";
            }
        });
    }

    /// <summary>Takes this source's word for the disagreement, and reloads the side-by-side view.</summary>
    public Task ChooseSourceAsync(
        SourceConflictResponse conflict,
        MetadataSourceName source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conflict);

        return Series is { } series
            ? RunAsync(async () => ReplaceSeries(
                await sourceConflicts.ChooseAsync(series.SeriesId, conflict, source, cancellationToken)))
            : Task.CompletedTask;
    }

    /// <summary>Hands the disagreement back to the usual rules.</summary>
    public Task ResetSourceAsync(SourceConflictResponse conflict, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conflict);

        return Series is { } series
            ? RunAsync(async () => ReplaceSeries(
                await sourceConflicts.ResetAsync(series.SeriesId, conflict, cancellationToken)))
            : Task.CompletedTask;
    }

    /// <summary>
    /// Prints a problem the page caught before any call was made, on the same line a
    /// refused write prints on.
    /// </summary>
    public void ReportProblem(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Message = message;
        Changed?.Invoke();
    }

    public Task RemoveCoverAsync(VolumeResponse volume, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(volume);

        return Series is null
            ? Task.CompletedTask
            : RunAsync(async () =>
                ReplaceSeries(await client.RemoveCoverAsync(volume.VolumeId, cancellationToken)));
    }

    /// <summary>"Add volume" on a series kept by hand: the next whole number.</summary>
    public Task AddVolumeAsync(CancellationToken cancellationToken = default) =>
        Series is { } series
            ? RunAsync(async () => ReplaceSeries(
                await client.AddVolumeAsync(series.SeriesId, number: null, cancellationToken)))
            : Task.CompletedTask;

    /// <summary>"Remove volume" on a volume added by hand. The caller confirms first.</summary>
    public Task RemoveVolumeAsync(VolumeResponse volume, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(volume);

        return Series is { } series
            ? RunAsync(async () => ReplaceSeries(
                await client.RemoveVolumeAsync(series.SeriesId, volume.VolumeId, cancellationToken)))
            : Task.CompletedTask;
    }

    private Task<bool> SetStateAsync(
        TrackingStatus? trackingStatus,
        string? notes,
        CancellationToken cancellationToken) =>
        Series is { } series
            ? RunAsync(async () => ReplaceSeries(await client.SetStateAsync(
                series.SeriesId, trackingStatus, notes, cancellationToken)))
            : Task.FromResult(false);

    /// <summary>Clears the new-volume badge, here and on the server.</summary>
    private Task MarkSeenAsync(CancellationToken cancellationToken) =>
        Series is { } series
            ? RunAsync(async () =>
            {
                await client.MarkSeenAsync(series.SeriesId, cancellationToken);

                // Cleared locally rather than re-fetched: the acknowledgement changes one
                // flag, and the library re-reads its cards on every visit anyway.
                Series = series with { HasNewVolume = false };
            })
            : Task.CompletedTask;

    /// <summary>
    /// The saving flag, the message and the change notification in one place, so no
    /// caller can forget one of the three.
    /// </summary>
    // ImportState.RunAsync is the same wrapper for the same reason; this one answers
    // whether the write landed, which is the one thing a caller ever asks it.
    private async Task<bool> RunAsync(Func<Task> operation)
    {
        IsSaving = true;
        Message = null;
        Changed?.Invoke();

        try
        {
            await operation();
            return true;
        }
        catch (HttpRequestException exception)
        {
            // Either the API refused, in which case the client wrote the sentence, or it
            // could not be reached at all. Rendering the failure is handling it.
            Message = exception.Message;
            return false;
        }
        finally
        {
            IsSaving = false;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Swaps in the series every write answers with — counts, volumes and all.
    /// </summary>
    // Nothing is recomputed here. The counts, the next-to-buy number and the tile states
    // are the server's derivation (SeriesDetailProjection), and this container holding a
    // second copy of that rule was two implementations of one thing, hand-synced.
    private void ReplaceSeries(SeriesDetailResponse? updated)
    {
        if (updated is null)
        {
            return;
        }

        // The write endpoints project the series without its LN ↔ manga link — only the
        // GET loads that — so the one the page arrived with is carried across rather than
        // vanishing on the first click.
        Series = updated with { Relation = updated.Relation ?? Series?.Relation };
    }
}
