using Microsoft.Extensions.Caching.Hybrid;

namespace LnShelf.Api.Features.Metadata.Services;

/// <inheritdoc />
public sealed class OpenLibraryVolumeEnrichmentSource(
    OpenLibraryClient client,
    HybridCache cache,
    IOptions<OpenLibraryOptions> options,
    ILogger<OpenLibraryVolumeEnrichmentSource> logger) : IVolumeEnrichmentSource
{
    public string SourceName => "OpenLibrary";

    private readonly HybridCacheEntryOptions _cacheOptions = new()
    {
        Expiration = options.Value.CacheDuration,
        LocalCacheExpiration = options.Value.CacheDuration,
        Flags = HybridCacheEntryFlags.DisableDistributedCache,
    };

    public async Task<VolumeEnrichment?> GetByIsbnAsync(string isbn, CancellationToken cancellationToken)
    {
        var normalized = Isbn.Normalize(isbn)
            ?? throw new ArgumentException($"'{isbn}' is not an ISBN.", nameof(isbn));

        // Cached for a day: an edition record that has existed for years is not about
        // to change, and one run can ask for the same ISBN twice — discovery verifies
        // candidates that a later enrichment pass then reads again.
        return await cache.GetOrCreateAsync(
            $"openlibrary:isbn:{normalized}",
            (Source: this, Isbn: normalized),
            static (state, token) => state.Source.FetchAsync(state.Isbn, token),
            _cacheOptions,
            cancellationToken: cancellationToken);
    }

    private async ValueTask<VolumeEnrichment?> FetchAsync(string isbn, CancellationToken cancellationToken)
    {
        var edition = await client.GetEditionAsync(isbn, cancellationToken);
        if (edition is null)
        {
            return null;
        }

        var enrichment = OpenLibraryEditionMapper.ToEnrichment(edition, client.CoverUrl);

        logger.LogInformation(
            "Open Library {RecordKey} for ISBN {Isbn}: date {ReleaseDate} (raw {RawPublishDate}), "
            + "cover {HasCover}, publisher {Publisher}",
            enrichment.SourceRecordKey,
            isbn,
            enrichment.ReleaseDate,
            enrichment.RawPublishDate,
            enrichment.CoverImageUrl is not null,
            enrichment.Publisher);

        return enrichment;
    }
}
