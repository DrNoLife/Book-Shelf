namespace LnShelf.Api.Features.Import.Services;

/// <inheritdoc />
public sealed class ImportQueryService(
    LnShelfDbContext dbContext,
    ISeriesMetadataSource metadataSource,
    ISeriesCoverCache coverCache,
    IOptions<ImportOptions> options) : IImportQueryService
{
    public async Task<ImportBatchResponse?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await dbContext.ImportBatches
            .AsNoTracking()
            .Include(candidate => candidate.Groups)
            .FirstOrDefaultAsync(candidate => candidate.Id == batchId, cancellationToken);

        return batch?.ToResponse();
    }

    public async Task<ImportQueueResponse?> GetQueueAsync(Guid batchId, CancellationToken cancellationToken)
    {
        // The whole queue in one round trip, candidates included — opening the page
        // must not cost a single AniList request (that is why candidates are
        // persisted at match time).
        var batch = await dbContext.ImportBatches
            .AsNoTracking()
            .Include(candidate => candidate.Groups).ThenInclude(group => group.Rows)
            .Include(candidate => candidate.Groups).ThenInclude(group => group.Candidates)
            .FirstOrDefaultAsync(candidate => candidate.Id == batchId, cancellationToken);

        return batch?.ToQueueResponse();
    }

    public async Task<IReadOnlyList<ImportBatchResponse>> GetRecentBatchesAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var batches = await dbContext.ImportBatches
            .AsNoTracking()
            .Include(batch => batch.Groups)
            .OrderByDescending(batch => batch.UploadedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return [.. batches.Select(batch => batch.ToResponse())];
    }

    public async Task<IReadOnlyList<ImportCandidateResponse>> SearchCandidatesAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var results = await metadataSource.SearchAsync(query, format: null, cancellationToken);
        var candidates = new List<ImportCandidateResponse>(results.Count);

        foreach (var result in results.Take(options.Value.CandidatesPerGroup))
        {
            // Covers are cached here rather than on GET /api/search, because this
            // is a deliberate submit-driven search over a handful of results — not
            // the general search endpoint, which must stay cheap.
            var coverObjectKey = await coverCache.EnsureCachedAsync(
                MetadataSourceName.AniList, result.AniListId, result.CoverImageUrl, cancellationToken);

            candidates.Add(new ImportCandidateResponse
            {
                AniListId = result.AniListId,
                TitleEnglish = result.TitleEnglish,
                TitleRomaji = result.TitleRomaji,
                TitleNative = result.TitleNative,
                Format = result.Format,
                PublicationStatus = result.PublicationStatus,
                VolumeCount = result.VolumeCount,
                StartYear = result.StartYear,
                CoverObjectKey = coverObjectKey,

                // A manual pick is the user's judgement, so the score is what the
                // title comparison says and nothing is filtered on it.
                Score = TitleSimilarityScorer.BestScore(
                    query, result.TitleEnglish, result.TitleRomaji, result.TitleNative),
            });
        }

        return candidates;
    }
}
