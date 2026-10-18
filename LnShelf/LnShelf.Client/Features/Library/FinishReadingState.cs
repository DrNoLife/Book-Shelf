namespace LnShelf.Client.Features.Library;

/// <summary>
/// "I finished Vol 18" and "+1 chapter" from the library, and their undo. One click records
/// the next volume or chapter as read; the toast's Undo puts back exactly what was there.
/// </summary>
// Its own container rather than a method on LibraryState: the library state reads, this
// writes, and the toast needs a place to read "what can be undone" from that outlives the
// card that was clicked — the card leaves the carousel the moment its series is caught up.
//
// A chapter that carries the reader past a volume's known end also leaves an offer to mark
// that volume read, shown on the same toast (owner decision 2026-10-14: offered, never forced).
// It is read from the chapter write's own answer, the whole series detail, so the library
// query stays as it is.
public sealed class FinishReadingState(HttpClient httpClient, LibraryState libraryState)
{
    /// <summary>The last volume or chapter recorded here, while its undo is still on offer.</summary>
    public ReadingReceipt? LastFinished { get; private set; }

    /// <summary>Volumes the last "+1 chapter" finished, offered to be marked read. Null when none.</summary>
    public VolumeReadOffer? Offer { get; private set; }

    /// <summary>True while a write is in flight; the buttons go inert so one tap is one write.</summary>
    public bool IsBusy { get; private set; }

    /// <summary>A refused or failed write, in a sentence. Cleared by the next attempt.</summary>
    public string? Message { get; private set; }

    public event Action? Changed;

    /// <summary>Records the entry's next volume or chapter as read, then refreshes the library around it.</summary>
    public async Task FinishAsync(ContinueReadingEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        ReadingReceipt finished = entry switch
        {
            NextVolumeEntry volume => new FinishedVolume(
                volume.NextVolumeId, volume.NextVolumeNumber, volume.Card.TitleEnglish, volume.NextVolumeReadStatus),
            NextChapterEntry chapter => new FinishedChapter(
                chapter.Card.SeriesId, chapter.NextChapter, chapter.Card.TitleEnglish, chapter.Card.LastChapterRead),
            _ => throw new ArgumentOutOfRangeException(nameof(entry), entry.GetType().Name, "Not a Continue reading entry."),
        };

        var answer = finished switch
        {
            FinishedVolume volume => await WriteVolumeAsync(volume.VolumeId, ReadStatus.Read, cancellationToken),
            FinishedChapter chapter => await WriteChapterAsync(chapter.SeriesId, chapter.Chapter, cancellationToken),
            _ => WriteAnswer.Failed,
        };

        if (answer.Written)
        {
            LastFinished = finished;
            Offer = finished is FinishedChapter && answer.Detail?.VolumesFinishedByChapter is { Count: > 0 } volumes
                ? new VolumeReadOffer(finished.SeriesTitle, volumes)
                : null;
            await libraryState.LoadAsync(cancellationToken);
        }

        Changed?.Invoke();
    }

    /// <summary>Marks the offered volumes read, one by one; the receipt that follows undoes them all.</summary>
    public async Task AcceptOfferAsync(CancellationToken cancellationToken = default)
    {
        if (Offer is not { } offer)
        {
            return;
        }

        var marked = new List<FinishedVolume>();

        // In order, stopping at a refusal: the volumes before it stay marked and the receipt
        // names exactly those, so Undo puts back what was written and nothing else.
        foreach (var volume in offer.Volumes)
        {
            if (!(await WriteVolumeAsync(volume.VolumeId, ReadStatus.Read, cancellationToken)).Written)
            {
                break;
            }

            marked.Add(new FinishedVolume(volume.VolumeId, volume.Number, offer.SeriesTitle, volume.ReadStatus));
        }

        if (marked.Count > 0)
        {
            Offer = null;
            LastFinished = new FinishedVolumes(marked, offer.SeriesTitle);
            await libraryState.LoadAsync(cancellationToken);
        }

        Changed?.Invoke();
    }

    /// <summary>Puts the last finished volume, chapter or offered volumes back the way they were.</summary>
    public async Task UndoAsync(CancellationToken cancellationToken = default)
    {
        if (LastFinished is not { } finished)
        {
            return;
        }

        var written = finished switch
        {
            FinishedVolume volume => (await WriteVolumeAsync(volume.VolumeId, volume.PreviousStatus, cancellationToken)).Written,
            FinishedChapter chapter => (await WriteChapterAsync(chapter.SeriesId, chapter.PreviousChapter, cancellationToken)).Written,
            FinishedVolumes volumes => await RestoreAsync(volumes, cancellationToken),
            _ => false,
        };

        if (written)
        {
            LastFinished = null;
            Offer = null;
            await libraryState.LoadAsync(cancellationToken);
        }

        Changed?.Invoke();
    }

    /// <summary>Withdraws the undo offer: the toast timed out or was dismissed.</summary>
    public void Dismiss()
    {
        if (LastFinished is null && Message is null && Offer is null)
        {
            return;
        }

        LastFinished = null;
        Offer = null;
        Message = null;
        Changed?.Invoke();
    }

    private async Task<bool> RestoreAsync(FinishedVolumes finished, CancellationToken cancellationToken)
    {
        foreach (var volume in finished.Volumes)
        {
            if (!(await WriteVolumeAsync(volume.VolumeId, volume.PreviousStatus, cancellationToken)).Written)
            {
                return false;
            }
        }

        return true;
    }

    // The same endpoints the series page writes through. Both answer with the whole series; the
    // library re-reads its cards afterwards, and only the chapter's answer is read, for the offer.
    private Task<WriteAnswer> WriteVolumeAsync(Guid volumeId, ReadStatus status, CancellationToken cancellationToken) =>
        PutAsync($"api/volumes/{volumeId}/read", new SetVolumeReadRequest { Status = status }, cancellationToken);

    // A null chapter is how the undo of a series' first recorded chapter clears it again.
    private Task<WriteAnswer> WriteChapterAsync(Guid seriesId, decimal? chapter, CancellationToken cancellationToken) =>
        PutAsync($"api/series/{seriesId}/chapter", new SetLastChapterReadRequest { LastChapterRead = chapter }, cancellationToken);

    private async Task<WriteAnswer> PutAsync<TRequest>(string uri, TRequest request, CancellationToken cancellationToken)
    {
        IsBusy = true;
        Message = null;
        Changed?.Invoke();

        try
        {
            using var response = await httpClient.PutAsJsonAsync(uri, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Message = $"Could not save that: {(int)response.StatusCode} {response.StatusCode}.";
                return WriteAnswer.Failed;
            }

            var detail = await response.Content.ReadFromJsonAsync<SeriesDetailResponse>(cancellationToken);
            return new WriteAnswer(true, detail);
        }
        catch (HttpRequestException)
        {
            // Rendering the failure is handling it; the shelf is unchanged.
            Message = "Could not reach the API. Nothing was changed.";
            return WriteAnswer.Failed;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Whether a write landed, and the series as it answered.</summary>
    private sealed record WriteAnswer(bool Written, SeriesDetailResponse? Detail)
    {
        public static WriteAnswer Failed { get; } = new(false, null);
    }
}
