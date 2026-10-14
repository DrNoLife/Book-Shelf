namespace LnShelf.Client.Features.Library;

/// <summary>What the library just recorded as read, with what it was before — enough to undo it.</summary>
public abstract record ReadingReceipt(string SeriesTitle)
{
    /// <summary>What was read, as the toast names it: "Vol 18", "Ch. 143", "Vols 10–12".</summary>
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

/// <summary>Volumes marked read from a chapter offer, each with the status it had.</summary>
public sealed record FinishedVolumes(IReadOnlyList<FinishedVolume> Volumes, string SeriesTitle)
    : ReadingReceipt(SeriesTitle)
{
    public override string Label => ChapterText.VolumesLabel([.. Volumes.Select(volume => volume.Number)]);
}

/// <summary>
/// The volumes a "+1 chapter" carried the reader past, offered to be marked read: the series
/// detail's <c>VolumesFinishedByChapter</c>, as the chapter write answered it.
/// </summary>
public sealed record VolumeReadOffer(string SeriesTitle, IReadOnlyList<VolumeResponse> Volumes)
{
    public IReadOnlyList<decimal> VolumeNumbers => [.. Volumes.Select(volume => volume.Number)];
}
