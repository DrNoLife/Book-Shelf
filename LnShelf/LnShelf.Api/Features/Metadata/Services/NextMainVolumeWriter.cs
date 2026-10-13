namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Creates the whole volumes straight past the highest main volume tracked once the store
/// catalogue lists them — 86's Vol. 14, a pre-order on Apple Books while MangaUpdates still
/// counts 13.
/// </summary>
// The third place sync creates a volume from a store, beside BonusVolumeWriter and
// WholeExtraVolumeWriter, and as narrow (CLAUDE.md — conflicting sources). The release source
// still owns the count: nothing here marks a volume released, and the count disagreement is
// still recorded every run. What the store adds is that the next book exists and when:
// - only once the release source states a count, so an unlinked or uncounted series never
//   grows from the store alone;
// - only the numbers straight after the highest main volume tracked, in order, stopping at
//   the first one the store does not list, because a gap is not something to guess past;
// - only a record the matcher confirmed as this series and medium, with a date no more than
//   AnnouncementHorizonMonths ahead;
// - never under a volume-count source choice, where the user has settled the count by hand.
// Nothing is deleted, and nothing but the new rows is written.
public sealed class NextMainVolumeWriter(ILogger<NextMainVolumeWriter> logger)
{
    /// <summary>How far ahead a store's date may be for its listing to count.</summary>
    // A store listing further out than a year is a placeholder rather than an announcement, and
    // a series gains at most a year of announced books from the store.
    public const int AnnouncementHorizonMonths = 12;

    /// <summary>
    /// Whether this series may gain next volumes from the store at all.
    /// </summary>
    // Public because the enrichment pass reads it to decide whether a series whose volumes
    // are all complete is still worth one store request.
    public static bool MayCreate(Series series)
    {
        ArgumentNullException.ThrowIfNull(series);

        return series.ReleaseSourceVolumeCount is not null
               && series.ChoiceFor(SyncDiscrepancyField.VolumeCount) is null;
    }

    /// <returns>
    /// The volumes created. The caller writes their facts in the same pass and decides
    /// whether they are news.
    /// </returns>
    public IReadOnlyList<Volume> Create(Series series, CatalogueMatch match, DateOnly today, SyncRun run)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(run);

        if (!MayCreate(series))
        {
            return [];
        }

        var latestAnnounced = today.AddMonths(AnnouncementHorizonMonths);
        var highestMain = series.Volumes
            .Where(volume => !volume.Number.IsBonusVolume)
            .Max(volume => (decimal?)volume.Number) ?? 0;
        var number = highestMain + 1;
        var created = new List<Volume>();

        while (true)
        {
            if (!match.UntrackedMainVolumes.TryGetValue(number, out var record)
                || record.ReleaseDate is not { } date
                || date > latestAnnounced)
            {
                break;
            }

            var volume = new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = number,

                // Past the count, so the count does not call it in print. Its date decides:
                // the projections call a volume out once its date has come.
                IsReleased = false,

                // Sync created it, so sync owns it.
                IsLocalOnly = false,
            };

            series.Volumes.Add(volume);
            created.Add(volume);
            run.NewVolumes++;

            logger.LogInformation(
                "{TitleEnglish}: added volume {VolumeNumber} from the store catalogue, dated {ReleaseDate}, "
                + "past the {TrackedCount} the release source counts",
                series.TitleEnglish,
                number.ToVolumeText(),
                date,
                series.ReleaseSourceVolumeCount);

            number++;
        }

        return created;
    }
}
