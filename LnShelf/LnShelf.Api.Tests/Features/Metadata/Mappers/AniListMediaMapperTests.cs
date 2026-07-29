using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Mappers;

/// <summary>
/// Dirty → clean, against captured real AniList responses (testing.md #4). The
/// mapping is where a source's oddities become the shelf's data, so the oddities
/// are the assertions: padded role strings, null English titles, and a volume
/// count that is not a volume list.
/// </summary>
public sealed class AniListMediaMapperTests
{
    private static IReadOnlyList<AniListMedia> MushokuSearchMedia =>
        AniListFixtures.LoadSearch(AniListFixtures.MushokuSearch).Page?.Media
        ?? throw new InvalidOperationException("The search fixture carries no media.");

    private static List<SeriesSearchResult> MapSearch() =>
        [.. MushokuSearchMedia.Select(AniListMediaMapper.ToSearchResult).OfType<SeriesSearchResult>()];

    [Fact]
    public void ToSearchResult_TheCapturedMushokuSearch_KeepsTheNovelAndTheMangaSeparate()
    {
        var results = MapSearch();

        var novel = results.Single(result => result.ExternalId == AniListFixtures.MushokuTenseiNovelId);
        var manga = results.Single(result => result.ExternalId == AniListFixtures.MushokuTenseiMangaId);

        // Same title, two series, told apart by format — feature-spec §4.2's
        // whole reason for surfacing LN and manga separately.
        novel.TitleEnglish.ShouldBe("Mushoku Tensei: Jobless Reincarnation");
        manga.TitleEnglish.ShouldBe("Mushoku Tensei: Jobless Reincarnation");

        novel.Format.ShouldBe(Format.LightNovel);
        novel.PublicationStatus.ShouldBe(PublicationStatus.Completed);
        novel.VolumeCount.ShouldBe(26);
        novel.StartYear.ShouldBe(2014);
        novel.TitleRomaji.ShouldBe("Mushoku Tensei: Isekai Ittara Honki Dasu");

        manga.Format.ShouldBe(Format.Manga);
        manga.PublicationStatus.ShouldBe(PublicationStatus.Ongoing);

        // Still releasing, so AniList publishes no total. Not zero — unknown.
        manga.VolumeCount.ShouldBeNull();
    }

    [Fact]
    public void ToSearchResult_TheCapturedMushokuSearch_MapsEveryRowItReturned()
    {
        var media = MushokuSearchMedia;

        // Every entry is a NOVEL, MANGA or ONE_SHOT, because the query asks for
        // those three; nothing should be dropped.
        MapSearch().Count.ShouldBe(media.Count);
        media.Count.ShouldBe(20);
    }

    [Fact]
    public void ToSearchResult_MediaWithNoEnglishTitle_FallsBackToRomaji()
    {
        var withoutEnglishTitle = MushokuSearchMedia
            .Where(media => string.IsNullOrWhiteSpace(media.Title?.English))
            .ToList();

        // The captured page really does contain some — that is the point.
        withoutEnglishTitle.ShouldNotBeEmpty();

        foreach (var media in withoutEnglishTitle)
        {
            var result = AniListMediaMapper.ToSearchResult(media).ShouldNotBeNull();
            result.TitleEnglish.ShouldBe(media.Title?.Romaji);
            result.TitleEnglish.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void ToSeriesMetadata_TheCapturedNovel_MapsEveryFieldTheShelfStores()
    {
        var metadata = AniListMediaMapper.ToSeriesMetadata(
            AniListFixtures.LoadMedia(AniListFixtures.MushokuTenseiNovel));

        metadata.ExternalId.ShouldBe(AniListFixtures.MushokuTenseiNovelId);
        metadata.TitleEnglish.ShouldBe("Mushoku Tensei: Jobless Reincarnation");
        metadata.TitleRomaji.ShouldBe("Mushoku Tensei: Isekai Ittara Honki Dasu");
        metadata.TitleNative.ShouldBe("無職転生 ～異世界行ったら本気だす～");
        metadata.Format.ShouldBe(Format.LightNovel);
        metadata.PublicationStatus.ShouldBe(PublicationStatus.Completed);
        metadata.StartYear.ShouldBe(2014);
        metadata.Genres.ShouldBe(["Adventure", "Drama", "Ecchi", "Fantasy"]);

        // The role in the payload is "Story " with a trailing space, and the ten
        // edges after it are translators and editors.
        metadata.Author.ShouldBe("Rifujin na Magonote");
        metadata.Illustrator.ShouldBe("Sirotaka");

        // AniList has no publisher field at all; MangaUpdates carries it (phase 2).
        metadata.Publisher.ShouldBeNull();
    }

    [Fact]
    public void ToSeriesMetadata_TheCapturedNovel_MaterialisesTheVolumeCountAsUndatedVolumes()
    {
        var metadata = AniListMediaMapper.ToSeriesMetadata(
            AniListFixtures.LoadMedia(AniListFixtures.MushokuTenseiNovel));

        metadata.Volumes.Count.ShouldBe(26);
        metadata.Volumes.Select(volume => volume.Number).ShouldBe(Enumerable.Range(1, 26));

        // AniList publishes a count, not volume entities: no per-volume dates and
        // no per-volume ids exist anywhere in its schema. MangaUpdates fills the
        // dates in phase 2.
        metadata.Volumes.ShouldAllBe(volume => volume.ReleaseDate == null);
        metadata.Volumes.ShouldAllBe(volume => volume.ExternalId == null);
    }

    [Theory]
    [InlineData("NOVEL", Format.LightNovel)]
    [InlineData("MANGA", Format.Manga)]
    // A one-shot is a one-volume manga, not a fourth format.
    [InlineData("ONE_SHOT", Format.Manga)]
    public void ToSearchResult_TrackedFormats_MapToTheShelfsFormat(string aniListFormat, Format expected) =>
        AniListMediaMapper.ToSearchResult(CreateMedia(format: aniListFormat))
            .ShouldNotBeNull()
            .Format.ShouldBe(expected);

    [Theory]
    [InlineData("MUSIC")]
    [InlineData("TV")]
    [InlineData(null)]
    public void ToSearchResult_UntrackedFormat_DropsTheRow(string? aniListFormat) =>
        AniListMediaMapper.ToSearchResult(CreateMedia(format: aniListFormat)).ShouldBeNull();

    [Theory]
    [InlineData("FINISHED", PublicationStatus.Completed)]
    [InlineData("HIATUS", PublicationStatus.Hiatus)]
    [InlineData("CANCELLED", PublicationStatus.Cancelled)]
    [InlineData("RELEASING", PublicationStatus.Ongoing)]
    // The shelf has no Announced status, and a sparse entry may carry no status.
    [InlineData("NOT_YET_RELEASED", PublicationStatus.Ongoing)]
    [InlineData(null, PublicationStatus.Ongoing)]
    public void ToSearchResult_PublicationStatus_MapsToTheShelfsStatus(
        string? aniListStatus, PublicationStatus expected) =>
        AniListMediaMapper.ToSearchResult(CreateMedia(status: aniListStatus))
            .ShouldNotBeNull()
            .PublicationStatus.ShouldBe(expected);

    [Fact]
    public void ToSeriesMetadata_ASingleMangakaCreditedAsStoryAndArt_IsBothAuthorAndIllustrator()
    {
        var media = CreateMedia(format: "MANGA");
        media.Staff = CreateStaff(("Story & Art", "Kentarou Miura"));

        var metadata = AniListMediaMapper.ToSeriesMetadata(media);

        metadata.Author.ShouldBe("Kentarou Miura");
        metadata.Illustrator.ShouldBe("Kentarou Miura");
    }

    [Fact]
    public void ToSeriesMetadata_StaffWithOnlyTranslators_LeavesAuthorAndIllustratorNull()
    {
        var media = CreateMedia();
        media.Staff = CreateStaff(
            ("Translator (English: vols 1, 3, 5)", "Paul Cuneo"),
            ("Editing (German)", "Melanie Höpfler"));

        var metadata = AniListMediaMapper.ToSeriesMetadata(media);

        metadata.Author.ShouldBeNull();
        metadata.Illustrator.ShouldBeNull();
    }

    [Fact]
    public void ToSeriesMetadata_NoVolumeCount_TracksNoVolumesRatherThanInventingOne()
    {
        var media = CreateMedia(format: "MANGA");
        media.Volumes = null;

        AniListMediaMapper.ToSeriesMetadata(media).Volumes.ShouldBeEmpty();
    }

    private static AniListMedia CreateMedia(string? format = "NOVEL", string? status = "RELEASING") => new()
    {
        Id = 1,
        Format = format,
        Status = status,
        Volumes = 3,
        Title = new AniListMediaTitle { English = "A Title", Romaji = "A Romaji Title" },
    };

    private static AniListStaffConnection CreateStaff(params (string Role, string Name)[] edges) => new()
    {
        Edges =
        [
            .. edges.Select(edge => new AniListStaffEdge
            {
                Role = edge.Role,
                Node = new AniListStaffNode { Name = new AniListStaffName { Full = edge.Name } },
            }),
        ],
    };
}
