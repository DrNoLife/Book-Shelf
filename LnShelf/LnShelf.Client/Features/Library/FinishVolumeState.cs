namespace LnShelf.Client.Features.Library;

/// <summary>A volume the library just marked read, with what it was before — enough to undo it.</summary>
public sealed record FinishedVolume(Guid VolumeId, decimal Number, string SeriesTitle, ReadStatus PreviousStatus);

/// <summary>
/// "I finished Vol 18" from the library, and its undo. One click marks the next volume read;
/// the toast's Undo puts back exactly the status it had (Unread or Reading).
/// </summary>
// Its own container rather than a method on LibraryState: the library state reads, this
// writes, and the toast needs a place to read "what can be undone" from that outlives the
// card that was clicked — the card leaves the carousel the moment its series is caught up.
public sealed class FinishVolumeState(HttpClient httpClient, LibraryState libraryState)
{
    /// <summary>The last volume marked read here, while its undo is still on offer.</summary>
    public FinishedVolume? LastFinished { get; private set; }

    /// <summary>True while a write is in flight; the buttons go inert so one tap is one write.</summary>
    public bool IsBusy { get; private set; }

    /// <summary>A refused or failed write, in a sentence. Cleared by the next attempt.</summary>
    public string? Message { get; private set; }

    public event Action? Changed;

    /// <summary>Marks the entry's next volume read, then refreshes the library around it.</summary>
    public async Task FinishAsync(ContinueReadingEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var finished = new FinishedVolume(
            entry.NextVolumeId, entry.NextVolumeNumber, entry.Card.TitleEnglish, entry.NextVolumeReadStatus);

        if (await WriteAsync(finished.VolumeId, ReadStatus.Read, cancellationToken))
        {
            LastFinished = finished;
            await libraryState.LoadAsync(cancellationToken);
        }

        Changed?.Invoke();
    }

    /// <summary>Puts the last finished volume back the way it was.</summary>
    public async Task UndoAsync(CancellationToken cancellationToken = default)
    {
        if (LastFinished is not { } finished)
        {
            return;
        }

        if (await WriteAsync(finished.VolumeId, finished.PreviousStatus, cancellationToken))
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

    // The same endpoint the series page's volume menu writes through; its answer (the whole
    // series) is not needed here, because the library re-reads its cards afterwards.
    private async Task<bool> WriteAsync(Guid volumeId, ReadStatus status, CancellationToken cancellationToken)
    {
        IsBusy = true;
        Message = null;
        Changed?.Invoke();

        try
        {
            using var response = await httpClient.PutAsJsonAsync(
                $"api/volumes/{volumeId}/read",
                new SetVolumeReadRequest { Status = status },
                cancellationToken);

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
