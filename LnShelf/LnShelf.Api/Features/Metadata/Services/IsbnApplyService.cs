namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Writes reviewed ISBNs onto their volumes. Idempotent, and additive only.
/// </summary>
// The second half of the human-in-the-loop split: discovery proposes into a file, a
// person deletes what they do not believe, and this applies what is left. It never
// clears an ISBN and never replaces one, for the same reason the import writer does
// not — a value already on a volume was either reviewed or imported, and this command
// has no grounds to overrule either.
public sealed class IsbnApplyService(LnShelfDbContext dbContext, ILogger<IsbnApplyService> logger)
{
    public async Task<IsbnApplyReport> ApplyAsync(
        IReadOnlyList<IsbnReviewInstruction> instructions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instructions);

        var seriesIds = instructions.Select(instruction => instruction.SeriesId).ToHashSet();

        // Keyed by the pair the file names a volume with, so applying a hundred reviewed
        // rows is a hundred lookups rather than a hundred scans of every volume of every
        // series mentioned. Tracked, not AsNoTracking: these are the rows being written.
        var volumes = await dbContext.Volumes
            .Where(volume => seriesIds.Contains(volume.SeriesId))
            .ToDictionaryAsync(
                volume => (volume.SeriesId, volume.Number), cancellationToken);

        var written = 0;
        var alreadyRecorded = 0;
        var withoutVolume = 0;

        foreach (var instruction in instructions)
        {
            if (!volumes.TryGetValue((instruction.SeriesId, instruction.VolumeNumber), out var volume))
            {
                logger.LogWarning(
                    "Reviewed ISBN {Isbn} names series {SeriesId} volume {VolumeNumber}, which the shelf "
                    + "does not track; skipped",
                    instruction.Isbn,
                    instruction.SeriesId,
                    instruction.VolumeNumber);

                withoutVolume++;
                continue;
            }

            if (volume.Isbn is not null)
            {
                alreadyRecorded++;
                continue;
            }

            volume.Isbn = instruction.Isbn;
            written++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var report = new IsbnApplyReport
        {
            Reviewed = instructions.Count,
            Written = written,
            AlreadyRecorded = alreadyRecorded,
            WithoutVolume = withoutVolume,
        };

        logger.LogInformation(
            "Applied reviewed ISBNs: {Reviewed} in the file, {Written} written, {AlreadyRecorded} already "
            + "recorded, {WithoutVolume} naming no tracked volume",
            report.Reviewed,
            report.Written,
            report.AlreadyRecorded,
            report.WithoutVolume);

        return report;
    }
}
