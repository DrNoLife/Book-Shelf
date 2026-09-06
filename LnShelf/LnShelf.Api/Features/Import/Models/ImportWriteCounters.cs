namespace LnShelf.Api.Features.Import.Models;

/// <summary>
/// What one commit did, accumulated as it goes.
/// </summary>
// Mutable on purpose — it is an accumulator, not a domain model — and the counts
// are the evidence that a second commit of the same export creates nothing.
public sealed class ImportWriteCounters
{
    public int SeriesCreated { get; set; }
    public int SeriesUpdated { get; set; }
    public int LocalOnlyVolumesCreated { get; set; }
    public int VolumeStatesWritten { get; set; }

    /// <summary>Volumes whose subtitle the export supplied and nothing else had.</summary>
    // Logged rather than reported on the wire: it is evidence about the metadata the
    // import saved a lookup on, which is a maintainer's question, not the user's.
    public int VolumeSubtitlesWritten { get; set; }
}
