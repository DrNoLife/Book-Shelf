namespace LnShelf.Api.Features.Covers.Services;

/// <summary>
/// Copies every cover from one store into another, verifying each copy by reading it back.
/// </summary>
// Written against two ICoverStores rather than against MinIO and a folder, so it knows nothing
// about either end and can be tested with any pair. Idempotent: a cover already in the target
// with the same bytes is skipped, so an interrupted run is finished by running it again.
public sealed class CoverMigrator(ILogger<CoverMigrator> logger)
{
    public async Task<CoverMigrationReport> RunAsync(
        ICoverStore source,
        ICoverStore target,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        int scanned = 0, copied = 0, skipped = 0, failed = 0;

        await foreach (var key in source.ListKeysAsync(string.Empty, cancellationToken))
        {
            scanned++;

            // One cover failing to copy must not abandon the rest; it is counted, logged and
            // retried by the next run.
            try
            {
                var outcome = await CopyAsync(source, target, key, dryRun, cancellationToken);

                if (outcome) { copied++; } else { skipped++; }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed++;
                logger.LogWarning(exception, "Could not migrate the cover {ObjectKey}", key);
            }
        }

        var report = new CoverMigrationReport(scanned, copied, skipped, failed);

        logger.LogInformation(
            "Cover migration {Mode}: {Scanned} scanned, {Copied} copied, {Skipped} skipped, {Failed} failed",
            dryRun ? "dry run" : "run", report.Scanned, report.Copied, report.Skipped, report.Failed);

        return report;
    }

    /// <returns>True when the cover was (or on a dry run would be) copied; false when it was already there.</returns>
    private static async Task<bool> CopyAsync(
        ICoverStore source,
        ICoverStore target,
        string key,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var original = await source.ReadAsync(key, cancellationToken)
            ?? throw new InvalidOperationException($"The cover {key} was listed but could not be read.");

        var existing = await target.ReadAsync(key, cancellationToken);
        if (existing is not null && existing.Content.AsSpan().SequenceEqual(original.Content))
        {
            return false;
        }

        if (dryRun)
        {
            return true;
        }

        // The type from the bytes, not from the old store's metadata: covers saved before every
        // cover went through the normaliser can be labelled one way and be another.
        await target.SaveAsync(key, original.Content, CoverImageSignature.ContentTypeOf(original.Content), cancellationToken);

        var copy = await target.ReadAsync(key, cancellationToken);
        if (copy is null || !copy.Content.AsSpan().SequenceEqual(original.Content))
        {
            throw new InvalidOperationException($"The cover {key} did not read back identical after copying.");
        }

        return true;
    }
}
