namespace LnShelf.EndToEnd.Tests.Fakes;

// The secondary sources know nothing. The journeys exercise the shelf, search and import,
// not the sync's precedence rules, which the unit suite covers against captured payloads.
// An empty answer is a state every source already has to cope with, so this takes the
// app down a real path rather than a test-only one.

public sealed class SilentMangaUpdatesSeriesQueryService : IMangaUpdatesSeriesQueryService
{
    public Task<IReadOnlyList<SeriesSearchResult>> SearchAsync(
        string query,
        Format? format,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SeriesSearchResult>>([]);

    public Task<MangaUpdatesSeriesMetadata?> GetSeriesAsync(long seriesId, CancellationToken cancellationToken) =>
        Task.FromResult<MangaUpdatesSeriesMetadata?>(null);
}

public sealed class SilentReleaseSource : IReleaseSource
{
    public string SourceName => "EndToEndReleases";

    public Task<IReadOnlyList<ReleaseSeriesCandidate>> SearchAsync(
        string title,
        Format format,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ReleaseSeriesCandidate>>([]);

    public Task<ReleaseSeriesInfo?> GetSeriesAsync(long externalId, CancellationToken cancellationToken) =>
        Task.FromResult<ReleaseSeriesInfo?>(null);
}

public sealed class SilentVolumeCatalogueSource : IVolumeCatalogueSource
{
    public string SourceName => "EndToEndCatalogue";

    public Task<IReadOnlyList<CatalogueVolume>> GetVolumesAsync(
        string seriesTitle,
        Format format,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CatalogueVolume>>([]);
}

public sealed class SilentVolumeEnrichmentSource : IVolumeEnrichmentSource
{
    public string SourceName => "EndToEndEnrichment";

    public Task<VolumeEnrichment?> GetByIsbnAsync(string isbn, CancellationToken cancellationToken) =>
        Task.FromResult<VolumeEnrichment?>(null);
}

public sealed class SilentIsbnCandidateSource : IIsbnCandidateSource
{
    public string SourceName => "EndToEndIsbnCandidates";

    public Task<IsbnCandidates> FindAsync(string seriesTitle, decimal volumeNumber, CancellationToken cancellationToken) =>
        Task.FromResult(IsbnCandidates.None);
}
