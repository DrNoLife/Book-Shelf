namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Creates the whole volumes past the main count that the release source counts as extras —
/// Solo Leveling's 14 and 15, which IZE Press notes as "13+2 Volumes" — once the store
/// catalogue lists them.
/// </summary>
// The second place sync creates a volume from a store, beside BonusVolumeWriter, and as narrow
// (CLAUDE.md — conflicting sources). The release source grants how many extras there are; the
// store only says which numbers they carry:
// - only up to the extras the release source counts, the bonus volumes already tracked
//   included, so "9 Volumes + 1 Extra" with Vol. 7.5 on the shelf grants nothing more;
// - only the numbers straight after the main count, in order, stopping at the first one the
//   store does not list, because a gap is not something to guess past;
// - only a record the matcher confirmed as this series and medium, and one with a date;
// - never under a volume-count source choice, where the user has settled the count by hand.
// Nothing is deleted, and nothing but the new rows is written.
public sealed class WholeExtraVolumeWriter(ILogger<WholeExtraVolumeWriter> logger)
{
    /// <param name="extraCount">The extras the release source counts beside its main volumes.</param>
    /// <returns>The volumes created, so the caller can write their facts in the same pass.</returns>
    public IReadOnlyList<Volume> Create(Series series, CatalogueMatch match, int extraCount, SyncRun run)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(run);

        if (extraCount <= 0
            || series.ReleaseSourceVolumeCount is not { } mainCount
            || series.ChoiceFor(SyncDiscrepancyField.VolumeCount) is not null)
        {
            return [];
        }

        var tracked = series.Volumes.Select(volume => volume.Number).ToHashSet();
        var placesFilled = series.Volumes.Count(volume => volume.Number.IsBonusVolume);
        var created = new List<Volume>();

        for (var number = mainCount + 1; number <= mainCount + extraCount && placesFilled < extraCount; number++)
        {
            if (tracked.Contains(number))
            {
                placesFilled++;
                continue;
            }

            if (!match.UntrackedMainVolumes.TryGetValue(number, out var record) || record.ReleaseDate is null)
            {
                break;
            }

            var volume = new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = number,

                // Past the main count, so the count does not call it in print. Its date
                // decides, as it does for a bonus volume past the count: the projections call
                // a volume out once its date has come.
                IsReleased = false,

                // Sync created it, so sync owns it.
                IsLocalOnly = false,
            };

            series.Volumes.Add(volume);
            created.Add(volume);
            placesFilled++;
            run.NewVolumes++;

            logger.LogInformation(
                "{TitleEnglish}: added volume {VolumeNumber} from the store catalogue, one of the "
                + "{ExtraCount} extras the release source counts past its {MainCount} volumes",
                series.TitleEnglish,
                number,
                extraCount,
                mainCount);
        }

        return created;
    }
}
