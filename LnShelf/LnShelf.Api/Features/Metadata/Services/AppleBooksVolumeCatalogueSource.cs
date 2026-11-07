using Microsoft.Extensions.Caching.Hybrid;

namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
public sealed class AppleBooksVolumeCatalogueSource(
    AppleBooksClient client,
    HybridCache cache,
    IOptions<AppleBooksOptions> options,
    ILogger<AppleBooksVolumeCatalogueSource> logger) : IVolumeCatalogueSource
{
    public string SourceName => "AppleBooks";

    private readonly HybridCacheEntryOptions _cacheOptions = new()
    {
        Expiration = options.Value.CacheDuration,
        LocalCacheExpiration = options.Value.CacheDuration,
        Flags = HybridCacheEntryFlags.DisableDistributedCache,
    };

    public async Task<IReadOnlyList<CatalogueVolume>> GetVolumesAsync(
        string seriesTitle,
        Format format,
        LookupBudget budget,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesTitle);
        ArgumentNullException.ThrowIfNull(budget);

        // Keyed on the normalised title, so the punctuation variants of one series' name
        // ("86―EIGHTY-SIX" and "86--EIGHTY-SIX") cannot cost two requests for one answer.
        // Cached for a day: a storefront listing for a book published years ago is not
        // about to change, and a sweep plus an on-demand refresh of the same series in one
        // evening is otherwise two identical requests.
        var cacheKey = $"applebooks:catalogue:{format}:{TitleNormalizer.Normalize(seriesTitle)}";

        return await cache.GetOrCreateAsync(
            cacheKey,
            (Source: this, SeriesTitle: seriesTitle, Format: format, Budget: budget),
            static (state, token) => state.Source.FetchAsync(state.SeriesTitle, state.Format, state.Budget, token),
            _cacheOptions,
            cancellationToken: cancellationToken);
    }

    private async ValueTask<IReadOnlyList<CatalogueVolume>> FetchAsync(
        string seriesTitle,
        Format format,
        LookupBudget budget,
        CancellationToken cancellationToken)
    {
        // Here, on a cache miss only, because this is the one place a request goes out.
        // Spent before it is sent, so a request that fails still counts: it reached the store.
        budget.Spend(1);

        var response = await client.SearchAsync(seriesTitle, format, cancellationToken);

        if (response is null)
        {
            return [];
        }

        var volumes = AppleBooksCatalogueMapper.ToCatalogueVolumes(response);

        logger.LogInformation(
            "Apple Books catalogue for {SeriesTitle} ({Format}): {BookCount} books of "
            + "{ResultCount} results, {DatedCount} dated, {DescribedCount} with a blurb",
            seriesTitle,
            format,
            volumes.Count,
            response.ResultCount,
            volumes.Count(volume => volume.ReleaseDate is not null),
            volumes.Count(volume => volume.Description is not null));

        return volumes;
    }
}
