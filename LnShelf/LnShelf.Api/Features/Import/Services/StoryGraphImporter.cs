using LnShelf.Api.Errors;

namespace LnShelf.Api.Features.Import.Services;

/// <inheritdoc />
public sealed class StoryGraphImporter(
    LnShelfDbContext dbContext,
    StoryGraphCsvReader csvReader,
    ISeriesMatcher seriesMatcher,
    ICoverCache coverCache,
    IOptions<ImportOptions> options,
    TimeProvider timeProvider,
    ILogger<StoryGraphImporter> logger) : IShelfImporter
{
    public string SourceName => "StoryGraph";

    public async Task<ImportBatchResponse> CreateBatchAsync(
        string fileName,
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);

        var books = await csvReader.ReadAsync(content, cancellationToken);

        if (books.Count == 0)
        {
            throw new InvalidInputException(
                "Nothing readable in that file. A StoryGraph export is a CSV with a Title column.");
        }

        if (books.Count > options.Value.MaximumRowCount)
        {
            throw new InvalidInputException(
                $"That file has {books.Count} rows; the limit is {options.Value.MaximumRowCount}.");
        }

        var batch = new ImportBatch
        {
            Id = Guid.CreateVersion7(),
            FileName = fileName,
            UploadedAt = timeProvider.GetUtcNow(),
            RowCount = books.Count,
        };

        // Sequential, not parallel. Politeness is the point: each group costs one
        // AniList search plus a handful of cover fetches, and a fan-out over 11
        // groups would spike both for no gain a human would notice.
        foreach (var grouping in books.GroupBy(book => book.SeriesKey, StringComparer.Ordinal))
        {
            batch.Groups.Add(await CreateGroupAsync(grouping, cancellationToken));
        }

        dbContext.ImportBatches.Add(batch);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = batch.ToResponse();

        logger.LogInformation(
            "Staged {FileName}: {RowCount} books in {GroupCount} series — {AutoMatched} auto-matched, {NeedsReview} to review",
            fileName,
            response.RowCount,
            response.GroupCount,
            response.AutoMatched,
            response.NeedsReview);

        return response;
    }

    private async Task<ImportGroup> CreateGroupAsync(
        IGrouping<string, StoryGraphBook> grouping,
        CancellationToken cancellationToken)
    {
        var seriesTitle = ChooseDisplayTitle(grouping);
        var match = await seriesMatcher.MatchAsync(seriesTitle, cancellationToken);

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
    /// The most frequent spelling in the group wins.
    /// </summary>
    // The rows of one group can be spelled differently and still normalise to the
    // same key: the real export writes the same series as "86—EIGHTY-SIX" ten times
    // and "86--EIGHTY-SIX" once. Showing the majority spelling means the queue
    // names the series the way its owner does.
    private static string ChooseDisplayTitle(IEnumerable<StoryGraphBook> books) =>
        books
            .GroupBy(book => book.SeriesTitle, StringComparer.Ordinal)
            .OrderByDescending(spelling => spelling.Count())
            .ThenBy(spelling => spelling.Key, StringComparer.Ordinal)
            .First()
            .Key;

    private static ImportRow ToRow(StoryGraphBook book) => new()
    {
        Id = Guid.CreateVersion7(),
        LineNumber = book.LineNumber,
        RawTitle = book.RawTitle,
        Author = book.Author,
        Isbn = book.Isbn,
        VolumeNumber = book.VolumeNumber,
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
            candidate.Result.ExternalId, candidate.Result.CoverImageUrl, cancellationToken);

        return new ImportCandidate
        {
            Id = Guid.CreateVersion7(),
            AniListId = candidate.Result.ExternalId,
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
