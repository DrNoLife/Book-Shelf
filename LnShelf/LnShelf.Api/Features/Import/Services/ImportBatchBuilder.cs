namespace LnShelf.Api.Features.Import.Services;

/// <summary>
/// Books in, a staged batch out: grouping, matching, cover caching and
/// persistence, with nothing in it that knows which product wrote the file.
/// </summary>
// Extracted from StoryGraphImporter, which was 160 lines of which about 130 were
// this. A second IShelfImporter would have been a copy of all of it, which is the
// opposite of what the seam promises — so the source-specific half (open the
// file, read its columns) stays in the importer and everything after the first
// clean ImportedBook lives here.
public sealed class ImportBatchBuilder(
    LnShelfDbContext dbContext,
    ISeriesMatcher seriesMatcher,
    ISeriesCoverCache coverCache,
    IOptions<ImportOptions> options,
    TimeProvider timeProvider,
    ILogger<ImportBatchBuilder> logger)
{
    /// <param name="sourceName">
    /// The importer's <see cref="IShelfImporter.SourceName"/> — it names the
    /// source in the refusal messages and in the log.
    /// </param>
    public async Task<ImportBatchResponse> BuildAsync(
        string sourceName,
        string fileName,
        IReadOnlyList<ImportedBook> books,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(books);

        if (books.Count == 0)
        {
            throw new InvalidInputException(
                $"Nothing readable in that file. A {sourceName} export is a CSV with a Title column.");
        }

        if (books.Count > options.Value.MaximumRowCount)
        {
            throw new InvalidInputException(
                $"That file has {books.Count} rows; the limit is {options.Value.MaximumRowCount}.");
        }

        // Fitted before grouping, so the group count below and the keys stored are the same.
        var fitted = books.Select(book => book.FittedToColumns()).ToList();

        // Refused before the first search: everything after this line costs upstream calls.
        var groupCount = fitted.Select(book => book.SeriesKey).Distinct(StringComparer.Ordinal).Count();
        if (groupCount > options.Value.MaximumGroupCount)
        {
            throw new InvalidInputException(
                $"That file has {groupCount} series; one import can take up to {options.Value.MaximumGroupCount}. "
                + "Split the export and import it in parts.");
        }

        var batch = new ImportBatch
        {
            Id = Guid.CreateVersion7(),
            FileName = fileName.Truncated(ImportColumnLimits.FileName),
            UploadedAt = timeProvider.GetUtcNow(),
            RowCount = books.Count,
        };

        // Sequential, not parallel. Politeness is the point: each group costs one
        // AniList search plus a handful of cover fetches, and a fan-out over 11
        // groups would spike both for no gain a human would notice.
        foreach (var grouping in fitted.GroupBy(book => book.SeriesKey, StringComparer.Ordinal))
        {
            batch.Groups.Add(await CreateGroupAsync(grouping, cancellationToken));
        }

        dbContext.ImportBatches.Add(batch);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = batch.ToResponse();

        logger.LogInformation(
            "Staged {FileName} from {SourceName}: {RowCount} books in {GroupCount} series — {AutoMatched} auto-matched, {NeedsReview} to review",
            fileName,
            sourceName,
            response.RowCount,
            response.GroupCount,
            response.AutoMatched,
            response.NeedsReview);

        return response;
    }

    private async Task<ImportGroup> CreateGroupAsync(
        IGrouping<string, ImportedBook> grouping,
        CancellationToken cancellationToken)
    {
        var seriesTitle = MostCommon(grouping.Select(book => book.SeriesTitle)) ?? grouping.Key;

        // The group's credits, not one row's: a single mistyped contributor should
        // not decide which series thirteen books belong to.
        var match = await seriesMatcher.MatchAsync(
            seriesTitle,
            MostCommon(grouping.Select(book => book.Author)),
            MostCommon(grouping.Select(book => book.Illustrator)),
            cancellationToken);

        var group = new ImportGroup
        {
            Id = Guid.CreateVersion7(),
            SeriesKey = grouping.Key,
            SeriesTitle = seriesTitle,
            Status = match.Status,
            Reason = match.Reason,
            MatchedAniListId = match.MatchedAniListId,
            MatchScore = match.MatchScore,
            TreatReadAsOwned = true,
        };

        foreach (var book in grouping.OrderBy(book => book.VolumeNumber ?? int.MaxValue))
        {
            group.Rows.Add(ToRow(book));
        }

        for (var rank = 0; rank < match.Candidates.Count; rank++)
        {
            group.Candidates.Add(await ToCandidateAsync(match.Candidates[rank], rank, cancellationToken));
        }

        return group;
    }

    /// <summary>
    /// The most frequent value in the group, or null when the group has none.
    /// </summary>
    // The rows of one group can be spelled differently and still normalise to the
    // same key: the real export writes the same series as "86—EIGHTY-SIX" ten times
    // and "86--EIGHTY-SIX" once. Showing the majority spelling means the queue
    // names the series the way its owner does — and the same rule picks the group's
    // author and illustrator, where a single row's typo should not outvote ten.
    //
    // The tie-break is the value itself, not file order, so two uploads of one
    // export never disagree about what the series is called.
    private static string? MostCommon(IEnumerable<string?> values) =>
        values
            .Where(value => value is not null)
            .GroupBy(value => value!, StringComparer.Ordinal)
            .OrderByDescending(spelling => spelling.Count())
            .ThenBy(spelling => spelling.Key, StringComparer.Ordinal)
            .FirstOrDefault()
            ?.Key;

    private static ImportRow ToRow(ImportedBook book) => new()
    {
        Id = Guid.CreateVersion7(),
        LineNumber = book.LineNumber,
        RawTitle = book.RawTitle,
        Author = book.Author,
        Isbn = book.Isbn,
        VolumeNumber = book.VolumeNumber,
        Subtitle = book.Subtitle,
        ReadStatus = book.ReadStatus,
        OwnedInExport = book.OwnedInExport,
        OwnedFormat = book.OwnedFormat,
        ReadAt = book.ReadAt,
    };

    private async Task<ImportCandidate> ToCandidateAsync(
        ScoredSeriesCandidate candidate,
        int rank,
        CancellationToken cancellationToken)
    {
        // Cached now, while the batch is being built, so opening the review queue
        // costs nothing and comparing candidates works with the CDN down. The key
        // is the AniList id, so this same cover becomes the series' cover the
        // moment this candidate is chosen.
        var coverObjectKey = await coverCache.EnsureCachedAsync(
            MetadataSourceName.AniList, candidate.Result.AniListId, candidate.Result.CoverImageUrl, cancellationToken);

        return new ImportCandidate
        {
            Id = Guid.CreateVersion7(),
            AniListId = candidate.Result.AniListId,
            TitleEnglish = candidate.Result.TitleEnglish,
            TitleRomaji = candidate.Result.TitleRomaji,
            TitleNative = candidate.Result.TitleNative,
            Format = candidate.Result.Format,
            PublicationStatus = candidate.Result.PublicationStatus,
            VolumeCount = candidate.Result.VolumeCount,
            StartYear = candidate.Result.StartYear,
            CoverObjectKey = coverObjectKey,
            Score = candidate.Score,
            Rank = rank,
        };
    }
}
