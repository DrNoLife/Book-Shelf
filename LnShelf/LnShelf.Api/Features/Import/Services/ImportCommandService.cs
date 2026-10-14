using LnShelf.Api.Features.Preferences.Abstractions;

namespace LnShelf.Api.Features.Import.Services;

/// <inheritdoc />
public sealed class ImportCommandService(
    LnShelfDbContext dbContext,
    ImportShelfWriter shelfWriter,
    IPreferencesService preferencesService,
    ISeriesMetadataSource metadataSource,
    ISeriesCoverCache coverCache,
    TimeProvider timeProvider,
    ILogger<ImportCommandService> logger) : IImportCommandService
{
    public async Task<ImportGroupResponse?> ResolveGroupAsync(
        Guid groupId,
        ResolveImportGroupRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var group = await dbContext.ImportGroups
            .Include(candidate => candidate.Rows)
            .Include(candidate => candidate.Candidates)
            .FirstOrDefaultAsync(candidate => candidate.Id == groupId, cancellationToken);

        if (group is null)
        {
            return null;
        }

        if (group.Status is ImportGroupStatus.Committed)
        {
            throw new ConflictException(
                $"'{group.SeriesTitle}' has already been committed to the shelf.");
        }

        group.TreatReadAsOwned = request.TreatReadAsOwned;

        if (request.Skip)
        {
            group.Status = ImportGroupStatus.Skipped;
            group.MatchedAniListId = null;
            group.MatchScore = null;
        }
        else if (request.AniListId is { } aniListId)
        {
            await EnsureCandidateExistsAsync(group, aniListId, cancellationToken);

            group.MatchedAniListId = aniListId;
            group.MatchScore = group.Candidates
                .FirstOrDefault(candidate => candidate.AniListId == aniListId)?.Score;
            group.Status = ImportGroupStatus.Resolved;
            group.Reason = ImportReviewReason.None;
        }
        else if (group.MatchedAniListId is null)
        {
            throw new InvalidInputException(
                $"'{group.SeriesTitle}' needs either a series to match or a decision to skip it.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Resolved import group {SeriesTitle} as {Status} (AniList {AniListId}, read implies owned: {TreatReadAsOwned})",
            group.SeriesTitle,
            group.Status,
            group.MatchedAniListId,
            group.TreatReadAsOwned);

        return group.ToResponse();
    }

    public async Task<ImportCommitResponse?> CommitAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await dbContext.ImportBatches
            .Include(candidate => candidate.Groups).ThenInclude(group => group.Rows)
            .FirstOrDefaultAsync(candidate => candidate.Id == batchId, cancellationToken);

        if (batch is null)
        {
            return null;
        }

        if (batch.CommittedAt is not null)
        {
            throw new ConflictException(
                "That batch has already been committed. Upload the export again to re-apply it.");
        }

        var stillWaiting = batch.Groups.Count(group => group.Status is ImportGroupStatus.NeedsReview);
        if (stillWaiting > 0)
        {
            // Refused rather than partially applied: a half-committed batch leaves
            // no honest state to report, and the queue exists precisely so this
            // decision is the user's.
            throw new ConflictException(
                $"{stillWaiting} series still need a decision. Resolve or skip them first.");
        }

        var toApply = batch.Groups
            .Where(group => group.Status is ImportGroupStatus.AutoMatched or ImportGroupStatus.Resolved
                            && group.MatchedAniListId is not null)
            .ToList();

        // Checked for every series before anything is written, for the same reason the
        // waiting groups are: a refusal halfway through would leave half a batch applied.
        var unplaceable = toApply
            .GroupBy(group => group.MatchedAniListId!.Value)
            .SelectMany(UnplaceableBookFinder.Find)
            .ToList();

        if (unplaceable.Count > 0)
        {
            throw new ConflictException(
                $"{unplaceable.Count} book(s) have no volume number, but the series they were matched to " +
                $"has numbered volumes, so there is no volume to put them on: {string.Join("; ", unplaceable)}. " +
                "Skip their group or match it to another series, then commit again.");
        }

        var counters = new ImportWriteCounters();
        var appliedAt = timeProvider.GetUtcNow();

        // Once for the whole commit: every series it creates starts in the same unit, and a big
        // export would otherwise read the same row once per new series.
        var preferences = await preferencesService.GetAsync(cancellationToken);

        // Grouped by AniList id, which is what merges the two spellings of one
        // series — a real export lists Slime and Overlord under both their English
        // and their Japanese titles, and they are one shelf entry each.
        foreach (var seriesGroups in toApply.GroupBy(group => group.MatchedAniListId!.Value))
        {
            await shelfWriter.ApplyAsync(
                seriesGroups.Key, [.. seriesGroups], counters, preferences.DefaultProgressUnit, cancellationToken);
        }

        foreach (var group in toApply)
        {
            group.Status = ImportGroupStatus.Committed;
        }

        batch.CommittedAt = appliedAt;
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new ImportCommitResponse
        {
            BatchId = batch.Id,
            SeriesCreated = counters.SeriesCreated,
            SeriesUpdated = counters.SeriesUpdated,
            LocalOnlyVolumesCreated = counters.LocalOnlyVolumesCreated,
            VolumeStatesWritten = counters.VolumeStatesWritten,
            GroupsCommitted = toApply.Count,
            GroupsSkipped = batch.Groups.Count(group => group.Status is ImportGroupStatus.Skipped),
        };

        logger.LogInformation(
            "Committed batch {BatchId}: {SeriesCreated} series created, {SeriesUpdated} updated, "
            + "{LocalOnlyVolumesCreated} local-only volumes added, {VolumeStatesWritten} volume states written, "
            + "{VolumeSubtitlesWritten} subtitles taken from the export",
            response.BatchId,
            response.SeriesCreated,
            response.SeriesUpdated,
            response.LocalOnlyVolumesCreated,
            response.VolumeStatesWritten,
            counters.VolumeSubtitlesWritten);

        return response;
    }

    /// <summary>
    /// A pick from the manual search will not be among the stored candidates, so
    /// it is added — the queue must show what was chosen, and the commit reads the
    /// score from there.
    /// </summary>
    private async Task EnsureCandidateExistsAsync(
        ImportGroup group,
        int aniListId,
        CancellationToken cancellationToken)
    {
        if (group.Candidates.Any(candidate => candidate.AniListId == aniListId))
        {
            return;
        }

        var metadata = await metadataSource.GetSeriesAsync(aniListId, cancellationToken)
            ?? throw new InvalidInputException($"AniList has no series with id {aniListId}.");

        var coverObjectKey = await coverCache.EnsureCachedAsync(
            MetadataSourceName.AniList, aniListId, metadata.CoverImageUrl, cancellationToken);

        group.Candidates.Add(new ImportCandidate
        {
            Id = Guid.CreateVersion7(),
            AniListId = aniListId,
            TitleEnglish = metadata.TitleEnglish,
            TitleRomaji = metadata.TitleRomaji,
            TitleNative = metadata.TitleNative,
            Format = metadata.Format,
            PublicationStatus = metadata.PublicationStatus,
            VolumeCount = metadata.Volumes.Count > 0 ? metadata.Volumes.Count : null,
            StartYear = metadata.StartYear,
            CoverObjectKey = coverObjectKey,
            Score = TitleSimilarityScorer.BestScore(
                group.SeriesTitle, metadata.TitleEnglish, metadata.TitleRomaji, metadata.TitleNative),

            // Last in the list: it was chosen by hand, not ranked by the matcher.
            Rank = group.Candidates.Count,
        });
    }
}
