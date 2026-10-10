namespace LnShelf.Client.Features.Search;

/// <summary>
/// The preview's "Start with": how many volumes are already read and owned when a series goes
/// on the shelf. Owned never drops below read — a read volume is one on the shelf.
/// </summary>
public sealed record StartingVolumes
{
    private StartingVolumes(int read, int owned, int maximum)
    {
        Read = read;
        Owned = owned;
        Maximum = maximum;
    }

    public int Read { get; }

    public int Owned { get; }

    /// <summary>The series' volume count, or the shared limit when the source publishes none.</summary>
    public int Maximum { get; }

    public bool CanReadMore => Read < Maximum;
    public bool CanReadFewer => Read > 0;
    public bool CanOwnMore => Owned < Maximum;
    public bool CanOwnFewer => Owned > Read;

    /// <summary>The line under the steppers, saying what Add is about to do.</summary>
    public string Hint => Owned switch
    {
        0 => "New to it? Leave both at 0 — it goes on Plan to start.",
        1 => "Volume 1 is filled in for you. Fine-tune later.",
        _ => $"Volumes 1–{Owned} are filled in for you. Fine-tune later.",
    };

    /// <summary>Both at zero, for a series nobody has started.</summary>
    public static StartingVolumes For(SeriesSearchResultResponse result)
    {
        ArgumentNullException.ThrowIfNull(result);

        // A count of zero is no count: AniList has no series with no volumes, only series it
        // has not counted yet.
        var maximum = result.VolumeCount is > 0 and var volumeCount
            ? volumeCount
            : AddSeriesRequest.InitialCountLimitWithoutVolumeCount;

        return new StartingVolumes(0, 0, maximum);
    }

    /// <summary>One more read raises owned with it.</summary>
    public StartingVolumes WithMoreRead() =>
        CanReadMore ? new StartingVolumes(Read + 1, Math.Max(Owned, Read + 1), Maximum) : this;

    public StartingVolumes WithFewerRead() =>
        CanReadFewer ? new StartingVolumes(Read - 1, Owned, Maximum) : this;

    public StartingVolumes WithMoreOwned() =>
        CanOwnMore ? new StartingVolumes(Read, Owned + 1, Maximum) : this;

    /// <summary>Stops at read: a read volume cannot be un-owned from here.</summary>
    public StartingVolumes WithFewerOwned() =>
        CanOwnFewer ? new StartingVolumes(Read, Owned - 1, Maximum) : this;
}
