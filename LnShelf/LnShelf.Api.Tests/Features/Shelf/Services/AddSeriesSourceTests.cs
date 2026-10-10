namespace LnShelf.Api.Tests.Features.Shelf.Services;

/// <summary>
/// The one rule behind the 400 on <c>POST /api/series</c>: exactly one source id.
/// </summary>
// Worth its own tests because the rule is the only thing standing between the endpoint and a
// guess. The two ids are different catalogues' names, and the same shelf holds series linked
// to one, the other or both — so a request naming both is not a helpful hint about the same
// series, it is two possible answers.
public sealed class AddSeriesSourceTests
{
    [Fact]
    public void Of_AnAniListId_IsAnAniListAdd()
    {
        AddSeriesSource.Of(new AddSeriesRequest { AniListId = 85470, Format = Format.LightNovel })
            .ShouldBe(MetadataSourceName.AniList);
    }

    [Fact]
    public void Of_AMangaUpdatesId_IsAMangaUpdatesAdd()
    {
        AddSeriesSource.Of(new AddSeriesRequest { MangaUpdatesId = 17992364412, Format = Format.LightNovel })
            .ShouldBe(MetadataSourceName.MangaUpdates);
    }

    [Fact]
    public void Of_NeitherId_IsRefusedRatherThanGuessedAt()
    {
        Action act = () => AddSeriesSource.Of(new AddSeriesRequest { Format = Format.LightNovel });

        // InvalidInputException is the 400: the caller sent something unusable, which is a
        // normal event and not a defect here.
        act.ShouldThrow<InvalidInputException>().Message.ShouldContain("exactly one");
    }

    [Fact]
    public void Of_BothIds_IsAlsoRefused()
    {
        Action act = () => AddSeriesSource.Of(new AddSeriesRequest
        {
            AniListId = 85470,
            MangaUpdatesId = 17992364412,
            Format = Format.LightNovel,
        });

        act.ShouldThrow<InvalidInputException>();
    }
}
