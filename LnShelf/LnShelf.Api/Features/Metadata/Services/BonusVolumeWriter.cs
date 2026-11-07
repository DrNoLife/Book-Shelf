namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Creates the bonus volumes a store catalogue lists between a series' main volumes —
/// Torture Princess Vol. 7.5 — so the facts pass can enrich them like any other volume.
/// </summary>
// One of the two places sync creates a volume from a store rather than from a count (the
// other is WholeExtraVolumeWriter), and the exception is narrow on purpose (CLAUDE.md — conflicting sources): only a fractional
// number, only one the matcher confirmed as this series and medium, only within the main
// volumes already tracked. The release source still owns how many main volumes there are,
// and it also owns how many extras there are, which bounds this writer the same way it
// bounds WholeExtraVolumeWriter:
// - only up to the extras the release source counts, the bonus and whole extras already
//   tracked included, so "9 Volumes + 1 Extra" with Vol. 7.5 on the shelf grants nothing
//   more, however many other "Vol. N.5" rows the store lists;
// - never under a volume-count source choice, where the user has settled the count by hand.
// Lowest numbers first when the store lists more than there are places for: no rule says
// which one is the real extra, and a stable choice beats a different one every night.
public sealed class BonusVolumeWriter(ILogger<BonusVolumeWriter> logger)
{
    /// <param name="extraCount">The extras the release source counts beside its main volumes.</param>
    /// <returns>The volumes created, so the caller can write their facts in the same pass.</returns>
    public IReadOnlyList<Volume> Create(Series series, CatalogueMatch match, int extraCount, SyncRun run)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(run);

        if (extraCount <= 0 || series.ChoiceFor(SyncDiscrepancyField.VolumeCount) is not null)
        {
            return [];
        }

        var placesLeft = Math.Max(extraCount - ExtraVolumes.CountTracked(series, extraCount), 0);
        var created = new List<Volume>();

        foreach (var number in match.BonusVolumes.Keys.Order().Take(placesLeft))
        {
            var volume = new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = number,

                // The release source's count says the main volumes up to it are in print, and
                // a bonus volume that sits among them is too — the rule MarkReleasedThrough
                // applies to every volume. Set now rather than on the next sweep, because a
                // later flip would raise the new-volume badge for a book that has been out
                // for years. A bonus volume past the count waits for its date, like a main one.
                IsReleased = series.ReleaseSourceVolumeCount is { } count && number <= count,

                // Sync created it, so sync owns it.
                IsLocalOnly = false,
            };

            series.Volumes.Add(volume);
            created.Add(volume);
            run.NewVolumes++;

            logger.LogInformation(
                "{TitleEnglish}: added bonus volume {VolumeNumber} from the store catalogue",
                series.TitleEnglish,
                number.ToVolumeText());
        }

        return created;
    }
}
