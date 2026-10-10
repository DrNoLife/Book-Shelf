namespace LnShelf.Api.Features.Shelf.Services;

/// <summary>
/// The add's "Start with": how far into a series the reader already is when it goes on the
/// shelf — volumes 1..<see cref="ReadCount"/> read, 1..<see cref="OwnedCount"/> owned.
/// </summary>
public sealed record SeriesStartingState
{
    private SeriesStartingState(int readCount, int ownedCount)
    {
        ReadCount = readCount;
        OwnedCount = ownedCount;
    }

    public int ReadCount { get; }

    /// <summary>Never below <see cref="ReadCount"/>.</summary>
    public int OwnedCount { get; }

    /// <summary>Nothing to write: the series goes on the shelf exactly as a plain add leaves it.</summary>
    // Owned alone decides it, because owned is never below read.
    public bool IsUntouched => OwnedCount == 0;

    /// <summary>
    /// The request's counts, checked as far as the request alone allows — before any source is
    /// asked for the series.
    /// </summary>
    public static SeriesStartingState From(AddSeriesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var readCount = request.InitialReadCount ?? 0;
        var ownedCount = request.InitialOwnedCount ?? 0;

        if (readCount < 0)
        {
            throw new InvalidInputException("A series cannot start with fewer than 0 volumes read.");
        }

        if (ownedCount < 0)
        {
            throw new InvalidInputException("A series cannot start with fewer than 0 volumes owned.");
        }

        // Raised rather than refused: the start-with control counts a read volume as one on
        // the shelf, so "read 3, own 1" can only mean three owned.
        return new SeriesStartingState(readCount, Math.Max(readCount, ownedCount));
    }

    /// <summary>
    /// Writes the starting state onto a series that is being added and has not been saved yet,
    /// so the series and the reader's place in it land in one save.
    /// </summary>
    /// <exception cref="InvalidInputException">The counts run past the series' volumes.</exception>
    public void ApplyTo(Series series)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (IsUntouched)
        {
            return;
        }

        EnsureWithinVolumeCount(series);
        AddMissingVolumes(series);

        // No date for either: when these were read or bought is exactly what the add does not
        // know, and today would be a date the stats page and the purchase history believe.
        BulkVolumeStateWriter.ApplyThrough(series, ReadCount, ReadStatus.Read, owned: null, stampedOn: null);
        BulkVolumeStateWriter.ApplyThrough(series, OwnedCount, read: null, owned: true, stampedOn: null);

        // The tracking status follows the start: anything read is a series being read. Set,
        // not promoted, because this series is brand new — there is no choice of the
        // reader's to overrule yet.
        series.UserState ??= SeriesUserState.Untouched(series.Id);
        series.UserState.TrackingStatus = ReadCount > 0 ? TrackingStatus.Reading : TrackingStatus.PlanToStart;
    }

    private void EnsureWithinVolumeCount(Series series)
    {
        var knownCount = series.Volumes.Max(volume => (int?)volume.Number);

        if (knownCount is { } volumeCount && OwnedCount > volumeCount)
        {
            throw new InvalidInputException(
                $"{series.TitleEnglish} has {volumeCount} volumes, so it cannot start with {OwnedCount}.");
        }

        if (knownCount is null && OwnedCount > AddSeriesRequest.InitialCountLimitWithoutVolumeCount)
        {
            throw new InvalidInputException(
                $"{series.TitleEnglish} has no volume count yet, so it can start with at most "
                + $"{AddSeriesRequest.InitialCountLimitWithoutVolumeCount} volumes.");
        }
    }

    /// <summary>
    /// A series whose source publishes no volume count arrives with no volumes at all; the
    /// ones the reader says they have are created so there is something to mark.
    /// </summary>
    // Local-only, as the import creates them for the same situation: the source does not
    // know these volumes, so a later sync reconciles them by number rather than owning them.
    private void AddMissingVolumes(Series series)
    {
        var missingNumbers = Enumerable.Range(1, OwnedCount)
            .Except(series.Volumes.Select(volume => volume.Number))
            .ToList();

        foreach (var number in missingNumbers)
        {
            series.Volumes.Add(new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = number,
                IsLocalOnly = true,
            });
        }
    }
}
