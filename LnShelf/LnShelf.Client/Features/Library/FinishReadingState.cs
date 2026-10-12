namespace LnShelf.Client.Features.Library;

/// <summary>What the library just recorded as read, with what it was before — enough to undo it.</summary>
public abstract record ReadingReceipt(string SeriesTitle)
{
    /// <summary>What was read, as the toast names it: "Vol 18", "Ch. 143".</summary>
    public abstract string Label { get; }
}

/// <summary>A volume the library just marked read, and the status it had (Unread or Reading).</summary>
public sealed record FinishedVolume(Guid VolumeId, decimal Number, string SeriesTitle, ReadStatus PreviousStatus)
    : ReadingReceipt(SeriesTitle)
{
    public override string Label => $"Vol {Number.ToVolumeText()}";
}

/// <summary>A chapter the library just recorded as the last read, and the one recorded before (or none).</summary>
public sealed record FinishedChapter(Guid SeriesId, decimal Chapter, string SeriesTitle, decimal? PreviousChapter)
    : ReadingReceipt(SeriesTitle)
{
    public override string Label => $"Ch. {Chapter.ToVolumeText()}";
}

/// <summary>
/// "I finished Vol 18" and "+1 chapter" from the library, and their undo. One click records
/// the next volume or chapter as read; the toast's Undo puts back exactly what was there.
/// </summary>
// Its own container rather than a method on LibraryState: the library state reads, this
// writes, and the toast needs a place to read "what can be undone" from that outlives the
// card that was clicked — the card leaves the carousel the moment its series is caught up.
public sealed class FinishReadingState(HttpClient httpClient, LibraryState libraryState)
{
    /// <summary>The last volume or chapter recorded here, while its undo is still on offer.</summary>
    public ReadingReceipt? LastFinished { get; private set; }

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

        var written = finished switch
        {
            FinishedVolume volume => await WriteVolumeAsync(volume.VolumeId, ReadStatus.Read, cancellationToken),
            FinishedChapter chapter => await WriteChapterAsync(chapter.SeriesId, chapter.Chapter, cancellationToken),
            _ => false,
        };

        if (written)
        {
            LastFinished = finished;
            await libraryState.LoadAsync(cancellationToken);
        }

        Changed?.Invoke();
    }

    /// <summary>Puts the last finished volume or chapter back the way it was.</summary>
    public async Task UndoAsync(CancellationToken cancellationToken = default)
    {
        if (LastFinished is not { } finished)
        {
            return;
        }

        var written = finished switch
        {
            FinishedVolume volume => await WriteVolumeAsync(volume.VolumeId, volume.PreviousStatus, cancellationToken),
            FinishedChapter chapter => await WriteChapterAsync(chapter.SeriesId, chapter.PreviousChapter, cancellationToken),
            _ => false,
        };

        if (written)
        {
            LastFinished = null;
            await libraryState.LoadAsync(cancellationToken);
        }

        Changed?.Invoke();
    }

    /// <summary>Withdraws the undo offer: the toast timed out or was dismissed.</summary>
    public void Dismiss()
    {
        if (LastFinished is null && Message is null)
        {
            return;
        }

        LastFinished = null;
        Message = null;
        Changed?.Invoke();
    }

    // The same endpoints the series page writes through; their answer (the whole series) is
    // not needed here, because the library re-reads its cards afterwards.
    private Task<bool> WriteVolumeAsync(Guid volumeId, ReadStatus status, CancellationToken cancellationToken) =>
        PutAsync($"api/volumes/{volumeId}/read", new SetVolumeReadRequest { Status = status }, cancellationToken);

    // A null chapter is how the undo of a series' first recorded chapter clears it again.
    private Task<bool> WriteChapterAsync(Guid seriesId, decimal? chapter, CancellationToken cancellationToken) =>
        PutAsync($"api/series/{seriesId}/chapter", new SetLastChapterReadRequest { LastChapterRead = chapter }, cancellationToken);

    private async Task<bool> PutAsync<TRequest>(string uri, TRequest request, CancellationToken cancellationToken)
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
                return false;
            }

            return true;
        }
        catch (HttpRequestException)
        {
            // Rendering the failure is handling it; the shelf is unchanged.
            Message = "Could not reach the API. Nothing was changed.";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
