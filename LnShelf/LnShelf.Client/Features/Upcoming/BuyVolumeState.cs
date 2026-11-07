using LnShelf.Shared.Releases;

namespace LnShelf.Client.Features.Upcoming;

/// <summary>
/// What "I bought Vol 13" has marked owned from the Upcoming page during this visit, per
/// series — enough to undo it.
/// </summary>
/// <param name="Entry">The out-now line as it was first bought from, so a series that is now
/// fully owned can still be drawn as "on your shelf" with its Undo.</param>
/// <param name="Volumes">The volumes marked owned here, in the order they were bought.</param>
public sealed record BoughtSeries(UpcomingEntryResponse Entry, IReadOnlyList<BoughtVolume> Volumes);

/// <summary>One volume "I bought Vol 13" marked owned.</summary>
public sealed record BoughtVolume(Guid VolumeId, decimal Number);

/// <summary>
/// "I bought Vol 13" on an out-now card, and its undo. One click marks the line's first
/// unbought volume owned and re-reads the Upcoming groups.
/// </summary>
// Its own container rather than methods on UpcomingState, the way FinishReadingState sits
// beside LibraryState: that one reads, this writes, and the record of what was bought has to
// outlive the out-now line itself, which leaves the response the moment the series is caught up.
public sealed class BuyVolumeState(HttpClient httpClient, UpcomingState upcomingState)
{
    private readonly Dictionary<Guid, BoughtSeries> _bought = [];

    /// <summary>True while a write is in flight; the buttons go inert so one tap is one write.</summary>
    public bool IsBusy { get; private set; }

    /// <summary>A refused or failed write, in a sentence. Cleared by the next attempt.</summary>
    public string? Message { get; private set; }

    public event Action? Changed;

    /// <summary>The series bought from here this visit.</summary>
    public IReadOnlyCollection<BoughtSeries> Bought => _bought.Values;

    public BoughtSeries? BoughtFrom(Guid seriesId) => _bought.GetValueOrDefault(seriesId);

    /// <summary>Marks the line's first unbought volume owned, then refreshes the groups.</summary>
    public async Task BuyNextAsync(UpcomingEntryResponse entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var volume = new BoughtVolume(entry.VolumeIds[0], entry.VolumeNumbers[0]);

        // A line drawn before the last re-read can still offer a volume already bought here;
        // buying it twice would record it twice and never reach the next one.
        if (_bought.GetValueOrDefault(entry.SeriesId)?.Volumes.Any(bought => bought.VolumeId == volume.VolumeId) == true)
        {
            return;
        }

        await RunAsync(async () =>
        {
            if (await WriteAsync(volume.VolumeId, owned: true, cancellationToken))
            {
                var bought = _bought.GetValueOrDefault(entry.SeriesId) ?? new BoughtSeries(entry, []);
                _bought[entry.SeriesId] = bought with { Volumes = [.. bought.Volumes, volume] };
                await upcomingState.LoadAsync();
            }
        });
    }

    /// <summary>Puts every volume bought from this series here back to not owned.</summary>
    public async Task UndoAsync(Guid seriesId, CancellationToken cancellationToken = default)
    {
        if (_bought.GetValueOrDefault(seriesId) is not { } bought)
        {
            return;
        }

        await RunAsync(async () =>
        {
            // Latest first, and stop at the first refusal: what is still recorded is then exactly
            // what is still owned, so a second Undo picks up where the first one stopped.
            var remaining = bought.Volumes.ToList();
            while (remaining.Count > 0 && await WriteAsync(remaining[^1].VolumeId, owned: false, cancellationToken))
            {
                remaining.RemoveAt(remaining.Count - 1);
            }

            if (remaining.Count == 0)
            {
                _bought.Remove(seriesId);
            }
            else
            {
                _bought[seriesId] = bought with { Volumes = remaining };
            }

            await upcomingState.LoadAsync();
        });
    }

    /// <summary>
    /// Drops the record when the page is left: the Undo is for the slip of a thumb on this
    /// visit, not a history, and a returning reader should see the shelf as it is.
    /// </summary>
    public void Forget()
    {
        _bought.Clear();
        Message = null;
    }

    /// <summary>
    /// The busy flag around a whole operation: the writes and the re-read of the groups after them.
    /// </summary>
    // Not around each write alone. The re-read raises Changed before it fetches, so a flag
    // cleared after the PUT put the button back on the old line, and a second tap bought the
    // same volume again instead of the next one.
    private async Task RunAsync(Func<Task> operation)
    {
        // Two taps can both arrive before the first one's disabled button is rendered.
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        Message = null;
        Changed?.Invoke();

        try
        {
            await operation();
        }
        finally
        {
            IsBusy = false;
            Changed?.Invoke();
        }
    }

    // The endpoint the series page's ownership control writes through. It answers with the
    // whole series, which is not needed here: the Upcoming groups are re-read afterwards.
    // No format and no price: "I bought it" says nothing about either, and the volume menu
    // can add them later.
    private async Task<bool> WriteAsync(Guid volumeId, bool owned, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.PutAsJsonAsync(
                $"api/volumes/{volumeId}/owned",
                new SetVolumeOwnedRequest { Owned = owned },
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
    }
}
