using Microsoft.Extensions.Caching.Hybrid;

namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
public sealed class AniListAuthorWorksQueryService(
    LnShelfDbContext dbContext,
    AniListGraphQlClient client,
    ISeriesCoverCache coverCache,
    HybridCache cache,
    ILogger<AniListAuthorWorksQueryService> logger) : IAuthorWorksQueryService
{
    /// <summary>How many of the author's credits are asked for, before any are filtered out.</summary>
    private const int CreditLimit = 20;

    // A day, not the search's hour: an author's bibliography moves on the scale of months, and
    // every series page asks this question on every visit.
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(24),
        LocalCacheExpiration = TimeSpan.FromHours(24),
        Flags = HybridCacheEntryFlags.DisableDistributedCache,
    };

    public async Task<AuthorWorksResponse?> GetForSeriesAsync(Guid seriesId, CancellationToken cancellationToken)
    {
        var series = await dbContext.Series
            .AsNoTracking()
            .Where(candidate => candidate.Id == seriesId)
            .Select(candidate => new { candidate.AniListId, candidate.Author })
            .FirstOrDefaultAsync(cancellationToken);

        if (series is null)
        {
            return null;
        }

        if (series.AniListId is not { } aniListId)
        {
            return new AuthorWorksResponse { Author = series.Author };
        }

        // Keyed on the AniList id alone and holding AniList's answer alone, because the cache is
        // shared by every shelf: which works this reader already has is decided below, per
        // request, and never stored here.
        var works = await cache.GetOrCreateAsync(
            $"anilist:author-works:{aniListId}",
            (Source: this, AniListId: aniListId),
            static (state, token) => state.Source.FetchAsync(state.AniListId, token),
            CacheOptions,
            cancellationToken: cancellationToken);

        return new AuthorWorksResponse
        {
            Author = works.AuthorName ?? series.Author,
            Works = await MarkShelvedAsync(works.Works, cancellationToken),
        };
    }

    /// <summary>The reader's own copy of each work, where they have one.</summary>
    private async Task<IReadOnlyList<AuthorWorkResponse>> MarkShelvedAsync(
        IReadOnlyList<AuthorWork> works,
        CancellationToken cancellationToken)
    {
        var ids = works.Select(work => work.AniListId).ToList();

        // Owner-filtered like every Series query, so "on your shelf" is this reader's shelf.
        var shelved = await dbContext.Series
            .AsNoTracking()
            .Where(candidate => candidate.AniListId != null && ids.Contains(candidate.AniListId.Value))
            .Select(candidate => new { AniListId = candidate.AniListId!.Value, candidate.Id })
            .ToListAsync(cancellationToken);

        var shelfIds = shelved.ToDictionary(entry => entry.AniListId, entry => entry.Id);

        return
        [
            .. works.Select(work => new AuthorWorkResponse
            {
                AniListId = work.AniListId,
                TitleEnglish = work.Title,
                Format = work.Format,
                CoverObjectKey = work.CoverObjectKey,
                ShelfSeriesId = shelfIds.TryGetValue(work.AniListId, out var seriesId) ? seriesId : null,
            }),
        ];
    }

    private async ValueTask<AuthorWorks> FetchAsync(int aniListId, CancellationToken cancellationToken)
    {
        var data = await client.ExecuteAsync<AniListMediaData>(
            nameof(AniListQueries.AuthorWorks),
            AniListQueries.AuthorWorks,
            new() { ["id"] = aniListId, ["perPage"] = CreditLimit },
            cancellationToken);

        var works = AniListAuthorWorksMapper.ToAuthorWorks(data?.Media);

        // Cached locally once, as the search does with its covers, so the tiles render from the
        // shelf's own cover endpoint and never from AniList's CDN (design.md).
        var withCovers = new List<AuthorWork>(works.Works.Count);
        foreach (var work in works.Works)
        {
            var objectKey = await coverCache.EnsureCachedAsync(
                MetadataSourceName.AniList, work.AniListId, work.CoverImageUrl, cancellationToken);

            withCovers.Add(work with { CoverObjectKey = objectKey });
        }

        logger.LogInformation(
            "AniList lists {WorkCount} other works by {Author} for {AniListId}",
            withCovers.Count,
            works.AuthorName ?? "no credited author",
            aniListId);

        return works with { Works = withCovers };
    }
}
