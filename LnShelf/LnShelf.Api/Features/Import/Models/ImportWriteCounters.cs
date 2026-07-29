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
}
