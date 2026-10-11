using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Mappers;

/// <summary>
/// "More by {author}" out of a captured <c>AuthorWorks</c> response (testing.md #4): which
/// staff edge is the author, which of their credits are writing, and what is left out.
/// </summary>
public sealed class AniListAuthorWorksMapperTests
{
    private static AniListMedia Captured =>
        AniListFixtures.LoadMedia(AniListFixtures.MushokuTenseiNovelAuthorWorks);

    [Fact]
    public void ToAuthorWorks_TheCapturedMushokuNovel_NamesTheStoryCreditNotTheIllustrator()
    {
        // The first edge is "Story " (padded); Sirotaka's "Illustration" edge comes second.
        AniListAuthorWorksMapper.ToAuthorWorks(Captured).AuthorName.ShouldBe("Rifujin na Magonote");
    }

    [Fact]
    public void ToAuthorWorks_TheCapturedMushokuNovel_LeavesTheSeriesItselfOut()
    {
        var works = AniListAuthorWorksMapper.ToAuthorWorks(Captured).Works;

        works.ShouldNotContain(work => work.AniListId == AniListFixtures.MushokuTenseiNovelId);

        // Fourteen credits, one of them the novel the question was asked about.
        works.Count.ShouldBe(13);
    }

    [Fact]
    public void ToAuthorWorks_TheCapturedMushokuNovel_KeepsTheMangaAdaptationsAndTheOtherNovels()
    {
        var works = AniListAuthorWorksMapper.ToAuthorWorks(Captured).Works;

        // "Original Story" on the manga drawn from the novel counts as writing it.
        var manga = works.Single(work => work.AniListId == AniListFixtures.MushokuTenseiMangaId);
        manga.Format.ShouldBe(Format.Manga);
        manga.Title.ShouldBe("Mushoku Tensei: Jobless Reincarnation");

        // A different series by the same author, credited "Story" without the padding.
        works.ShouldContain(work => work.AniListId == 119987 && work.Format == Format.LightNovel);
    }

    [Fact]
    public void ToAuthorWorks_TheCapturedMushokuNovel_KeepsAniListsPopularityOrder()
    {
        var works = AniListAuthorWorksMapper.ToAuthorWorks(Captured).Works;

        works[0].AniListId.ShouldBe(AniListFixtures.MushokuTenseiMangaId);
    }

    [Fact]
    public void ToAuthorWorks_AWorkWithNoEnglishTitle_FallsBackToRomaji()
    {
        var works = AniListAuthorWorksMapper.ToAuthorWorks(Captured).Works;

        works.Single(work => work.AniListId == 142989)
            .Title.ShouldBe("Mushoku Tensei: Isekai Ittara Honki Dasu - Shitsui no Majutsushi-hen");
    }

    [Fact]
    public void ToAuthorWorks_TheIllustratorsCredits_NeverAppear()
    {
        // Sirotaka illustrated these two novels; the author did not write them.
        var works = AniListAuthorWorksMapper.ToAuthorWorks(Captured).Works;

        works.ShouldNotContain(work => work.AniListId == 216935);
        works.ShouldNotContain(work => work.AniListId == 95761);
    }

    [Fact]
    public void ToAuthorWorks_NoAuthorCredit_IsNone()
    {
        var media = new AniListMedia
        {
            Id = 1,
            Staff = new AniListStaffConnection
            {
                Edges =
                [
                    new AniListStaffEdge
                    {
                        Role = "Illustration",
                        Node = new AniListStaffNode { Name = new AniListStaffName { Full = "Someone" } },
                    },
                ],
            },
        };

        AniListAuthorWorksMapper.ToAuthorWorks(media).ShouldBe(AuthorWorks.None);
    }

    [Fact]
    public void ToAuthorWorks_AnUntrackedFormatOrATitlelessEntry_IsSkipped()
    {
        var media = new AniListMedia
        {
            Id = 1,
            Staff = new AniListStaffConnection
            {
                Edges =
                [
                    new AniListStaffEdge
                    {
                        Role = "Story & Art",
                        Node = new AniListStaffNode
                        {
                            Name = new AniListStaffName { Full = "A Mangaka" },
                            StaffMedia = new AniListStaffMediaConnection
                            {
                                Edges =
                                [
                                    Credit("Story & Art", 2, "MANGA", "Kept"),
                                    Credit("Story & Art", 3, "NOVEL_ARTBOOK", "Unknown format"),
                                    Credit("Story & Art", 4, "MANGA", title: null),
                                    Credit("Story & Art", 2, "MANGA", "Kept, twice"),
                                ],
                            },
                        },
                    },
                ],
            },
        };

        var works = AniListAuthorWorksMapper.ToAuthorWorks(media);

        works.AuthorName.ShouldBe("A Mangaka");
        works.Works.Select(work => work.AniListId).ShouldBe(new[] { 2 });
    }

    private static AniListStaffMediaEdge Credit(string role, int id, string format, string? title) => new()
    {
        StaffRole = role,
        Node = new AniListMedia
        {
            Id = id,
            Format = format,
            Title = title is null ? null : new AniListMediaTitle { English = title },
        },
    };
}
