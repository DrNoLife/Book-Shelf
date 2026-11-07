namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The search endpoint's read path: which source answered, each hit's cached cover, and the
/// degraded note, carried onto the wire.
/// </summary>
public sealed class SeriesSearchPresentationServiceTests
{
    private const string Query = "youjo senki";

    private readonly ISeriesSearchService _search = Substitute.For<ISeriesSearchService>();
    private readonly ISeriesCoverCache _coverCache = Substitute.For<ISeriesCoverCache>();

    [Fact]
    public async Task SearchAsync_EveryHit_GetsTheCoverKeyCachedForItsOwnSourceAndId()
    {
        Answer(MetadataSourceName.AniList, note: null, Hit(1, "https://cdn/1.jpg"), Hit(2, "https://cdn/2.jpg"));
        _coverCache.EnsureCachedAsync(MetadataSourceName.AniList, 1, "https://cdn/1.jpg", Arg.Any<CancellationToken>())
            .Returns("covers/anilist/1.jpg");
        _coverCache.EnsureCachedAsync(MetadataSourceName.AniList, 2, "https://cdn/2.jpg", Arg.Any<CancellationToken>())
            .Returns("covers/anilist/2.jpg");

        var response = await CreateService().SearchAsync(Query, Format.LightNovel, CancellationToken.None);

        response.Source.ShouldBe(MetadataSourceName.AniList);
        response.Results.Select(result => result.CoverObjectKey)
            .ShouldBe(["covers/anilist/1.jpg", "covers/anilist/2.jpg"]);
    }

    [Fact]
    public async Task SearchAsync_ACoverThatCouldNotBeCached_LeavesThatHitWithoutOneAndKeepsTheRest()
    {
        Answer(MetadataSourceName.AniList, note: null, Hit(1, "https://cdn/1.jpg"), Hit(2, coverImageUrl: null));
        _coverCache.EnsureCachedAsync(MetadataSourceName.AniList, 1, "https://cdn/1.jpg", Arg.Any<CancellationToken>())
            .Returns("covers/anilist/1.jpg");

        // What the cache answers when there was nothing to cache or caching failed.
        _coverCache.EnsureCachedAsync(MetadataSourceName.AniList, 2, null, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var response = await CreateService().SearchAsync(Query, Format.LightNovel, CancellationToken.None);

        // A cover is decoration: the hit without one is still offered, just without art.
        response.Results.Count.ShouldBe(2);
        response.Results[0].CoverObjectKey.ShouldBe("covers/anilist/1.jpg");
        response.Results[1].CoverObjectKey.ShouldBeNull();
    }

    [Fact]
    public async Task SearchAsync_TheFallbackAnswering_CarriesItsSourceIdsAndTheNote()
    {
        Answer(
            MetadataSourceName.MangaUpdates,
            FallbackSeriesSearchService.DegradedNote,
            Hit(24348008396, "https://cdn/mu.jpg", MetadataSourceName.MangaUpdates));
        _coverCache.EnsureCachedAsync(
                MetadataSourceName.MangaUpdates, 24348008396, "https://cdn/mu.jpg", Arg.Any<CancellationToken>())
            .Returns("covers/mangaupdates/24348008396.jpg");

        var response = await CreateService().SearchAsync(Query, Format.LightNovel, CancellationToken.None);

        // The page needs all three to send Add to the right source and explain the romaji titles.
        response.Source.ShouldBe(MetadataSourceName.MangaUpdates);
        response.DegradedNote.ShouldBe(FallbackSeriesSearchService.DegradedNote);

        var hit = response.Results.ShouldHaveSingleItem();
        hit.MangaUpdatesId.ShouldBe(24348008396);
        hit.AniListId.ShouldBeNull();
        hit.CoverObjectKey.ShouldBe("covers/mangaupdates/24348008396.jpg");
    }

    private SeriesSearchPresentationService CreateService() => new(_search, _coverCache);

    private void Answer(MetadataSourceName source, string? note, params SeriesSearchResult[] results) =>
        _search.SearchAsync(Query, Format.LightNovel, Arg.Any<CancellationToken>())
            .Returns(new SeriesSearchOutcome { Source = source, Results = results, DegradedNote = note });

    private static SeriesSearchResult Hit(
        long sourceId,
        string? coverImageUrl,
        MetadataSourceName source = MetadataSourceName.AniList) => new()
        {
            Source = source,
            SourceId = sourceId,
            TitleEnglish = $"Series {sourceId}",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
            CoverImageUrl = coverImageUrl,
        };
}
