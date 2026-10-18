using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Mappers;

/// <summary>
/// The two captured aggregates into "whole volume to chapters": English first, the
/// all-languages one filling and extending, and MangaDex's JSON quirks survived.
/// </summary>
public sealed class MangaDexAggregateMapperTests
{
    [Fact]
    public void ToChaptersByVolume_EmptyArrayForVolumes_IsNoVolumes()
    {
        var empty = MangaDexFixtures.LoadAggregate(MangaDexFixtures.EmptyAggregate);

        MangaDexAggregateMapper.ToChaptersByVolume(empty, empty).ShouldBeEmpty();
        MangaDexAggregateMapper.ToChaptersByVolume(null, null).ShouldBeEmpty();
    }

    // "none" holds the unassigned chapters, and Slime's "0" — whose chapters are an array, not
    // a keyed object — holds a prologue. Neither is a volume of any edition.
    [Fact]
    public void ToChaptersByVolume_NoneAndZero_AreLeftOut()
    {
        var chapters = MangaDexFixtures.ChaptersByVolume(MangaDexFixtures.SlimeEnglish, MangaDexFixtures.SlimeAll);

        chapters.Keys.Min().ShouldBe(1);
        chapters.Keys.Max().ShouldBe(31);
        chapters.Count.ShouldBe(31);
    }

    // English stops part-way through Slime's vol 24 (ch 106 only); the all-languages aggregate
    // carries the rest of it, and fills vols 25–31 that English lacks.
    [Fact]
    public void ToChaptersByVolume_EnglishStoppingMidVolume_IsExtendedFromAllLanguages()
    {
        var chapters = MangaDexFixtures.ChaptersByVolume(MangaDexFixtures.SlimeEnglish, MangaDexFixtures.SlimeAll);

        chapters[24].ShouldBe([106m, 107m, 108m, 109m]);
        chapters[25][0].ShouldBe(110m);
    }

    // The all-languages vol 4 of Frieren overlaps vols 3 and 5 (ch 26–39); English's is 28–37.
    // Only chapters past English's last are added, so the overlap below never arrives.
    [Fact]
    public void ToChaptersByVolume_EnglishVolume_WinsOverAnOverlappingAllLanguagesOne()
    {
        var chapters = MangaDexFixtures.ChaptersByVolume(MangaDexFixtures.FrierenEnglish, MangaDexFixtures.FrierenAll);

        chapters[4][0].ShouldBe(28m);
        chapters[4].ShouldNotContain(26m);
    }

    // Berserk's all-languages aggregate counts the prologue as chapters 1–16 where English says
    // 0.01–0.16. Those never enter an English volume below the next English start.
    [Fact]
    public void ToChaptersByVolume_AllLanguagesNumberingDiffers_StaysOutOfEnglishVolumes()
    {
        var chapters = MangaDexFixtures.ChaptersByVolume(MangaDexFixtures.BerserkEnglish, MangaDexFixtures.BerserkAll);

        chapters[1].ShouldBe([0.01m, 0.02m, 0.03m]);
        chapters[5][0].ShouldBe(0.15m);
        chapters[5].ShouldNotContain(15m);
    }

    // Chainsaw Man in English stops at vol 11; vols 12–20 come from all languages, decimals
    // ("44.2", "175.5") and the vol 1 stray (ch 232) as filed.
    [Fact]
    public void ToChaptersByVolume_VolumesEnglishLacks_ComeFromAllLanguagesWithDecimalsKept()
    {
        var chapters = MangaDexFixtures.ChaptersByVolume(MangaDexFixtures.ChainsawManEnglish, MangaDexFixtures.ChainsawManAll);

        chapters[1].ShouldContain(232m);
        chapters[12][0].ShouldBe(98m);
        chapters[19].ShouldContain(175.5m);
        chapters.ContainsKey(21).ShouldBeFalse();
        chapters[24].ShouldBe([230m]);
    }
}
