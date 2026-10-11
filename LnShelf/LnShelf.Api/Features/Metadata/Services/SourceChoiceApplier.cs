namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Puts a source choice's effect on the series the moment it is made, and takes it back when it
/// is reset or changed.
/// </summary>
// Pure and without I/O: the command service loads, saves and answers. The effect lands in the
// normal synced columns (Volume.ReleaseDate, Volume.IsReleased, Series.PublicationStatus), not
// in an overlay, because six projections work "is it out" out from those columns and an
// overlay would have to be joined into every one of them. The nightly sync then keeps the
// effect fresh through the same rules (ReleaseCountWriter, VolumeFactWriter,
// PublicationStatusWriter); this class only does the first application, so the page is right
// before the next sync rather than after it.
//
// Every choice is applied from *the value the user saw in the diff* — the newest recorded row
// — and never by calling a source.
public sealed class SourceChoiceApplier
{
    /// <param name="recorded">The newest recorded disagreement for the choice's key.</param>
    public void Apply(Series series, SourceChoice choice, SyncDiscrepancy? recorded)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(choice);

        // Every choice not listed is what automatic sync already does, so there is nothing to
        // change: MangaUpdates on a date or a count, AniList on a status.
        switch (choice)
        {
            case { Field: SyncDiscrepancyField.ReleaseDate, Source: not MetadataSourceName.MangaUpdates }:
                ApplyChosenDate(series, choice, recorded);
                break;

            case { Field: SyncDiscrepancyField.VolumeCount, Source: MetadataSourceName.AniList }:
                // The shelf's own list is the count: every synced volume is out. Nothing is
                // deleted or created, and the user's own act raises no badge.
                ReleaseCountWriter.MarkReleasedThrough(
                    series, series.Volumes.Max(volume => (decimal?)volume.Number) ?? 0, run: null, includeLocalOnly: false);
                break;

            case { Field: SyncDiscrepancyField.Status, Source: MetadataSourceName.MangaUpdates }:
                ApplyChosenStatus(series, choice, recorded);
                break;
        }
    }

    /// <summary>Undoes <see cref="Apply"/>, for a reset or before a different source is applied.</summary>
    /// <param name="recorded">The newest recorded disagreement for the choice's key.</param>
    public void Revert(Series series, SourceChoice choice, SyncDiscrepancy? recorded)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(choice);

        switch (choice)
        {
            case { Field: SyncDiscrepancyField.ReleaseDate, Source: not MetadataSourceName.MangaUpdates }:
                RevertChosenDate(series, choice);
                break;

            case { Field: SyncDiscrepancyField.VolumeCount, Source: MetadataSourceName.AniList }:
                RevertShelfCount(series, recorded);
                break;

            case { Field: SyncDiscrepancyField.Status, Source: MetadataSourceName.MangaUpdates }:
                RevertChosenStatus(series, choice);
                break;
        }
    }

    // The chosen source's date decides this volume: written even though the count says the
    // volume is out, and IsReleased is cleared so the date, not the count, says whether it is.
    // An unreadable recorded value (none today; a future format) writes nothing, and the next
    // refresh applies the choice from the source's fresh facts.
    private static void ApplyChosenDate(Series series, SourceChoice choice, SyncDiscrepancy? recorded)
    {
        var volume = VolumeOf(series, choice);

        if (volume is null
            || !DiscrepancyValueFormat.TryParseDate(ValueOf(recorded, choice.Source), out var date))
        {
            return;
        }

        volume.ReleaseDate = date;
        volume.ReleaseDateSource = choice.Source;
        volume.IsReleased = false;
    }

    // Back to what the count says. The date goes only if the chosen source wrote it; a date
    // from elsewhere was never the choice's to remove. The next refresh re-derives the rest: a
    // cleared date makes the volume worth asking both per-volume sources about again.
    private static void RevertChosenDate(Series series, SourceChoice choice)
    {
        var volume = VolumeOf(series, choice);

        if (volume is null)
        {
            return;
        }

        if (volume.ReleaseDateSource == choice.Source)
        {
            volume.ReleaseDate = null;
            volume.ReleaseDateSource = null;
        }

        // A standing AniList count choice says every synced volume is out, whatever
        // MangaUpdates' count reaches; resetting the date must not undo that.
        if (series.ChoiceFor(SyncDiscrepancyField.VolumeCount) is { Source: MetadataSourceName.AniList }
            && !volume.IsLocalOnly)
        {
            volume.IsReleased = true;
            return;
        }

        // The conflict can only have been recorded on a volume the count called out, so with no
        // count on record it is restored as out.
        volume.IsReleased = series.ReleaseSourceVolumeCount is not { } count || volume.Number <= count;
    }

    // What the choice displaced is kept on the choice, because the nightly sweep never re-reads
    // AniList: without it the shelf's own status would be gone for good.
    private static void ApplyChosenStatus(Series series, SourceChoice choice, SyncDiscrepancy? recorded)
    {
        choice.ReplacedValue = DiscrepancyValueFormat.Status(series.PublicationStatus);

        if (DiscrepancyValueFormat.TryParseStatus(ValueOf(recorded, MetadataSourceName.MangaUpdates), out var status))
        {
            series.PublicationStatus = status;
        }
    }

    private static void RevertChosenStatus(Series series, SourceChoice choice)
    {
        if (DiscrepancyValueFormat.TryParseStatus(choice.ReplacedValue, out var status))
        {
            series.PublicationStatus = status;
        }
    }

    // Only the volumes MangaUpdates' count does not reach lose their "out": the ones it does
    // reach were already out by its word. The count comes from the last refresh, else from the
    // newest recorded row; with neither there is nothing to restore it from, so it is left.
    private static void RevertShelfCount(Series series, SyncDiscrepancy? recorded)
    {
        var count = series.ReleaseSourceVolumeCount
                    ?? (DiscrepancyValueFormat.TryParseCount(ValueOf(recorded, MetadataSourceName.MangaUpdates), out var parsed)
                        ? parsed
                        : (int?)null);

        if (count is not { } sourceCount)
        {
            return;
        }

        foreach (var volume in series.Volumes.Where(volume => volume.Number > sourceCount && !volume.IsLocalOnly))
        {
            volume.IsReleased = false;
        }
    }

    private static Volume? VolumeOf(Series series, SourceChoice choice) =>
        series.Volumes.FirstOrDefault(volume => volume.Number == choice.VolumeNumber);

    private static string? ValueOf(SyncDiscrepancy? recorded, MetadataSourceName source) =>
        recorded?.Values.FirstOrDefault(value => value.Source == source)?.Value;
}
