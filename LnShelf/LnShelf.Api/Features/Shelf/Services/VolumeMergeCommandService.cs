namespace LnShelf.Api.Features.Shelf.Services;

/// <inheritdoc />
// Every check runs before the first write, so a refusal leaves the shelf exactly as it was.
// The target's synced metadata (date, ISBN, title, synced cover) is never touched: only user
// state moves, which is the one thing sync cannot put back on its own.
public sealed class VolumeMergeCommandService(
    LnShelfDbContext dbContext,
    ICoverStore coverStore,
    ILogger<VolumeMergeCommandService> logger) : IVolumeMergeCommandService
{
    public async Task<VolumeMergeReport> MergeAsync(VolumeMergeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.FromNumber == request.IntoNumber)
        {
            throw new InvalidInputException("--from and --into name the same volume.");
        }

        var series = await FindSeriesAsync(request, cancellationToken);
        var source = FindVolume(series, request.FromNumber);
        var target = FindVolume(series, request.IntoNumber);

        EnsureNotCountedBySync(series, source);
        EnsureNoSourceChoice(series, source);
        EnsureUntouched(target);
        await EnsureCoverOverrideExistsAsync(source, cancellationToken);

        var report = new VolumeMergeReport(
            series.TitleEnglish,
            source.Number,
            target.Number,
            MovedUserState: source.UserState is not null,
            MovedCoverOverride: source.UserState?.CoverOverrideObjectKey is not null,
            request.DryRun);

        if (!request.DryRun)
        {
            await MoveAndDeleteAsync(source, target, cancellationToken);
        }

        logger.LogInformation(
            "Volume merge on {SeriesId} (dry run: {DryRun}): {FromNumber} into {IntoNumber}, user state moved {MovedUserState}, cover override moved {MovedCoverOverride}",
            series.Id, request.DryRun, source.Number, target.Number, report.MovedUserState, report.MovedCoverOverride);

        return report;
    }

    /// <summary>
    /// Refuses to merge away a volume the release source still counts.
    /// </summary>
    // Sync creates every missing volume up to the release source's count, so deleting one
    // inside it would only last until the next night's sweep put it back, empty.
    private static void EnsureNotCountedBySync(Series series, Volume source)
    {
        if (series.ReleaseSourceVolumeCount is { } count && source.Number <= count)
        {
            throw new InvalidInputException(
                $"The release source counts {count} volumes, so sync would recreate volume "
                + $"{source.Number.ToVolumeText()} the next night. Nothing was merged.");
        }
    }

    private async Task MoveAndDeleteAsync(Volume source, Volume target, CancellationToken cancellationToken)
    {
        var moved = source.UserState;
        var replacedCoverKey = moved?.CoverOverrideObjectKey;
        var copiedCoverKey = await CopyCoverOverrideAsync(replacedCoverKey, target.Id, cancellationToken);

        if (moved is not null)
        {
            // An existing target row is untouched by now (EnsureUntouched), so overwriting it
            // loses nothing; the key is the volume id, so the source row cannot be re-pointed.
            var state = target.UserState ??= new VolumeUserState { VolumeId = target.Id };
            state.ReadStatus = moved.ReadStatus;
            state.ReadAt = moved.ReadAt;
            state.Owned = moved.Owned;
            state.OwnedFormat = moved.OwnedFormat;
            state.PurchasedAt = moved.PurchasedAt;
            state.PurchasePrice = moved.PurchasePrice;
            state.NotifyWhenDated = moved.NotifyWhenDated;
            state.CoverOverrideObjectKey = copiedCoverKey;

            dbContext.VolumeUserStates.Remove(moved);
        }

        dbContext.Volumes.Remove(source);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch when (copiedCoverKey is not null)
        {
            // Nothing points at the copy if the save failed, and the orphan sweep would never
            // find it: its key names the target, which is still alive.
            await coverStore.DeleteAsync(copiedCoverKey, CancellationToken.None);
            throw;
        }

        if (replacedCoverKey is not null)
        {
            await DeleteReplacedCoverAsync(replacedCoverKey, cancellationToken);
        }
    }

    /// <summary>
    /// Copies an uploaded cover to a key under the target volume, returning the new key.
    /// </summary>
    // A copy rather than the old key carried over: an override key names its volume, and the
    // covers endpoint and the orphan sweep both judge a key by that volume. Left under the
    // deleted volume's id, the cover would 404 at once and be swept as an orphan later.
    private async Task<string?> CopyCoverOverrideAsync(
        string? objectKey,
        Guid targetVolumeId,
        CancellationToken cancellationToken)
    {
        if (objectKey is null)
        {
            return null;
        }

        var cover = await coverStore.ReadAsync(objectKey, cancellationToken)
            ?? throw new InvalidOperationException($"The uploaded cover {objectKey} vanished during the merge.");

        var copiedKey = CoverObjectKey.ForVolumeOverride(targetVolumeId, Guid.CreateVersion7(), Path.GetExtension(objectKey));
        await coverStore.SaveAsync(copiedKey, cover.Content, cover.ContentType, cancellationToken);
        return copiedKey;
    }

    // Best effort, as when a cover is replaced through the API: the merge is saved, and an
    // object left behind is unreferenced and will be found by cover-orphan-sweep.
    private async Task DeleteReplacedCoverAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            await coverStore.DeleteAsync(objectKey, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not delete the merged-away cover {ObjectKey}; cover-orphan-sweep will", objectKey);
        }
    }

    private async Task<Series> FindSeriesAsync(VolumeMergeRequest request, CancellationToken cancellationToken)
    {
        var title = request.SeriesTitle.Trim();

        var matches = await dbContext.Series
            .Include(series => series.SourceChoices)
            .Include(series => series.Volumes).ThenInclude(volume => volume.UserState)
            .Where(series => series.TitleEnglish == title || series.TitleRomaji == title)
            .Where(series => request.Format == null || series.Format == request.Format)
            .ToListAsync(cancellationToken);

        return matches switch
        {
            [var only] => only,
            [] => throw new InvalidInputException($"No series on this shelf is titled '{title}'."),
            _ => throw new InvalidInputException(
                $"'{title}' matches {matches.Count} series ({string.Join(", ", matches.Select(series => series.Format))}); "
                + "pass --format to pick one."),
        };
    }

    private static Volume FindVolume(Series series, decimal number) =>
        series.Volumes.FirstOrDefault(volume => volume.Number == number)
        ?? throw new InvalidInputException($"{series.TitleEnglish} has no volume {number.ToVolumeText()}.");

    // A choice pinned to the volume number would either follow the merged-away volume into
    // nothing or silently start deciding the target's date. Neither is the user's call made
    // for them, so the merge waits until they reset it.
    private static void EnsureNoSourceChoice(Series series, Volume source)
    {
        if (series.SourceChoices.Any(choice => choice.VolumeNumber == source.Number))
        {
            throw new InvalidInputException(
                $"Volume {source.Number.ToVolumeText()} has a source choice. Reset it on the series page first.");
        }
    }

    // A row in its defaults is what touching and untouching a volume leaves behind; anything
    // beyond that is the user's, and the merge would overwrite it.
    private static void EnsureUntouched(Volume target)
    {
        if (target.UserState is { } state
            && (state.ReadStatus is not ReadStatus.Unread
                || state.ReadAt is not null
                || state.Owned
                || state.OwnedFormat is not null
                || state.PurchasedAt is not null
                || state.PurchasePrice is not null
                || state.NotifyWhenDated
                || state.CoverOverrideObjectKey is not null))
        {
            throw new InvalidInputException(
                $"Volume {target.Number.ToVolumeText()} already has user state of its own; merging would overwrite it.");
        }
    }

    // Checked up front so a dangling key refuses the merge instead of failing it halfway.
    private async Task EnsureCoverOverrideExistsAsync(Volume source, CancellationToken cancellationToken)
    {
        if (source.UserState?.CoverOverrideObjectKey is { } objectKey
            && !await coverStore.ExistsAsync(objectKey, cancellationToken))
        {
            throw new InvalidInputException(
                $"Volume {source.Number.ToVolumeText()}'s uploaded cover {objectKey} is missing from the store. Remove it first.");
        }
    }
}
