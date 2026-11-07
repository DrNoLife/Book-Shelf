using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// A search hit followed to the detail endpoint of the source that produced it.
/// </summary>
// The dispatch is the whole of this class, and getting it wrong is silent: a MangaUpdates id
// handed to AniList is just "no such series", which reads as a missing record rather than a bug.
public sealed class SearchHitResolverTests
{
    private readonly ISeriesMetadataSource _aniList = Substitute.For<ISeriesMetadataSource>();
    private readonly IMangaUpdatesSeriesQueryService _mangaUpdates = Substitute.For<IMangaUpdatesSeriesQueryService>();

    [Fact]
    public async Task ResolveAsync_AnAniListHit_AsksAniListOnlyAndResolvesItsDetail()
    {
        var media = AniListMediaMapper.ToSeriesMetadata(AniListFixtures.LoadMedia(AniListFixtures.MushokuTenseiNovel));
        _aniList.GetSeriesAsync(AniListFixtures.MushokuTenseiNovelId, Arg.Any<CancellationToken>()).Returns(media);

        var resolved = await CreateResolver().ResolveAsync(
            Hit(MetadataSourceName.AniList, AniListFixtures.MushokuTenseiNovelId), CancellationToken.None);

        var series = resolved.ShouldNotBeNull();
        series.Source.ShouldBe(MetadataSourceName.AniList);
        series.SourceId.ShouldBe(AniListFixtures.MushokuTenseiNovelId);
        await _mangaUpdates.DidNotReceiveWithAnyArgs().GetSeriesAsync(default, default);
    }

    [Fact]
    public async Task ResolveAsync_AMangaUpdatesHit_AsksMangaUpdatesOnlyAndResolvesItsDetail()
    {
        var metadata = MangaUpdatesSeriesMapper.ToSeriesMetadata(
            MangaUpdatesFixtures.LoadSeries(MangaUpdatesFixtures.YoujoSenkiSeries));
        _mangaUpdates.GetSeriesAsync(MangaUpdatesFixtures.YoujoSenkiSeriesId, Arg.Any<CancellationToken>())
            .Returns(metadata);

        var resolved = await CreateResolver().ResolveAsync(
            Hit(MetadataSourceName.MangaUpdates, MangaUpdatesFixtures.YoujoSenkiSeriesId), CancellationToken.None);

        var series = resolved.ShouldNotBeNull();
        series.Source.ShouldBe(MetadataSourceName.MangaUpdates);

        // An eleven-digit id, which only survives because the hit carries a long.
        series.SourceId.ShouldBe(MangaUpdatesFixtures.YoujoSenkiSeriesId);
        await _aniList.DidNotReceiveWithAnyArgs().GetSeriesAsync(default, default);
    }

    [Theory]
    [InlineData(MetadataSourceName.AniList)]
    [InlineData(MetadataSourceName.MangaUpdates)]
    public async Task ResolveAsync_TheSourceHasNoDetail_IsNull(MetadataSourceName source)
    {
        // Both substitutes answer null by default: the source no longer has that id.
        var resolved = await CreateResolver().ResolveAsync(Hit(source, 42), CancellationToken.None);

        resolved.ShouldBeNull();
    }

    [Theory]
    [InlineData(MetadataSourceName.OpenLibrary)]
    [InlineData(MetadataSourceName.AppleBooks)]
    public async Task ResolveAsync_AHitFromAPerVolumeSource_ThrowsRatherThanGuessing(MetadataSourceName source)
    {
        // Neither can be asked about a series, so a hit claiming one as its source is a defect.
        await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => CreateResolver().ResolveAsync(Hit(source, 42), CancellationToken.None));
    }

    private SearchHitResolver CreateResolver() => new(_aniList, _mangaUpdates);

    private static SeriesSearchResult Hit(MetadataSourceName source, long sourceId) => new()
    {
        Source = source,
        SourceId = sourceId,
        TitleEnglish = "A series",
        Format = Format.LightNovel,
        PublicationStatus = PublicationStatus.Ongoing,
    };
}
