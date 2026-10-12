namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Creates the bonus volumes a store catalogue lists between a series' main volumes —
/// Torture Princess Vol. 7.5 — so the facts pass can enrich them like any other volume.
/// </summary>
// One of the two places sync creates a volume from a store rather than from a count (the
// other is WholeExtraVolumeWriter), and the exception is narrow on purpose (CLAUDE.md — conflicting sources): only a fractional
// number, only one the matcher confirmed as this series and medium, only within the main
// volumes already tracked. The release source still owns how many main volumes there are.
public sealed class BonusVolumeWriter(ILogger<BonusVolumeWriter> logger)
{
    /// <returns>The volumes created, so the caller can write their facts in the same pass.</returns>
    public IReadOnlyList<Volume> Create(Series series, CatalogueMatch match, SyncRun run)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(run);

        var created = new List<Volume>();

        foreach (var number in match.BonusVolumes.Keys.Order())
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
