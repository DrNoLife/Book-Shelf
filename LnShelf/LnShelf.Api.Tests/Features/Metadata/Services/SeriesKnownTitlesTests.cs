namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// Which of a series' names are worth asking a US storefront about. Every title below is a
/// real MangaUpdates <c>associated</c> entry for a series on this shelf.
/// </summary>
// The filter is the load-bearing half: it decides how many store requests a series costs and
// which of its names ever get tried at all. It is also the half that must never turn into an
// English-picking rule, which is what the refusal cases below pin down.
public sealed class SeriesKnownTitlesTests
{
    [Theory]
    // Romaji, which is what MangaUpdates files a large share of series under. Worth a
    // request: some storefront listings really do use it.
    [InlineData("Kono Subarashii Sekai ni Shukufuku o!")]
    [InlineData("Youjo Senki")]
    [InlineData("Isekai Goumon-hime")]

    // The English name, which is the one the US store usually answers to.
    [InlineData("Konosuba - God's Blessing on This Wonderful World!")]
    [InlineData("Torture Princess: Fremd Torturchen")]

    // German. Kept deliberately: this is a script filter, not a language one, and a German
    // title costs one request to disprove.
    [InlineData("Meine Wiedergeburt als Schleim in einer anderen Welt")]

    // Mixed, and majority Latin — one of Youjo Senki's own. The Latin half is a title a
    // store could conceivably carry.
    [InlineData("戰記 Deus lo vult")]

    // No letters at all, only digits and punctuation: nothing here a store cannot read.
    [InlineData("86")]
    [InlineData("86--EIGHTY-SIX")]
    public void IsLatinScript_ATitleTheStoreCouldMatch_IsKept(string title) =>
        SeriesKnownTitles.IsLatinScript(title).ShouldBeTrue();

    [Theory]
    // The four scripts MangaUpdates mixes into the same unlabelled bag. A US storefront
    // search cannot match a character of any of them, so asking costs a request for a
    // certainty.
    [InlineData("幼女戦記 (小説)")]
    [InlineData("Военная хроника маленькой девочки")]
    [InlineData("إهداء هذا العالم الرائع بالبركات!")]
    [InlineData("关于我转生后成为史莱姆的那件事简介")]
    [InlineData("戦記")]

    // Nothing at all is not a title.
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsLatinScript_ATitleTheStoreCannotMatch_IsRefused(string? title) =>
        SeriesKnownTitles.IsLatinScript(title).ShouldBeFalse();

    [Fact]
    public void Alternatives_TheRealKonosubaBag_KeepsTheThreeLatinOnesInTheSourcesOrder()
    {
        var alternatives = SeriesKnownTitles.Alternatives(
            "Kono Subarashii Sekai ni Shukufuku o!",
            [
                "Gifting this Wonderful World with Blessings!",
                "Kono Subarashii Sekai ni Shukufuku wo!",
                "Konosuba - God's Blessing on This Wonderful World!",
                "Коносуба: Да благословят боги этот прекрасный мир!",
                "إهداء هذا العالم الرائع بالبركات!",
                "この素晴らしい世界に祝福を!",
            ]);

        // The source's order is kept rather than reranked, because nothing here knows which
        // one the store will recognise — that is the store's answer to give. The English name
        // is third, which is exactly why arbitration cannot stop at the first attempt.
        alternatives.ShouldBe(
        [
            "Gifting this Wonderful World with Blessings!",
            "Kono Subarashii Sekai ni Shukufuku wo!",
            "Konosuba - God's Blessing on This Wonderful World!",
        ]);
    }

    [Fact]
    public void Alternatives_ATitleThatOnlyRepunctuatesAnother_IsDroppedAsARepeat()
    {
        // Both are real, and both are one title: Torture Princess' bag carries the hyphenated
        // and unhyphenated spellings side by side. The catalogue source keys its cache on the
        // normalised title, so the two would have been one request whatever this did —
        // collapsing them here spends one attempt instead of two discovering that.
        SeriesKnownTitles.Alternatives(
                "Fremd Torturchen",
                ["Isekai Goumon Hime", "Isekai Goumon-hime", "Torture Princess: Fremd Torturchen"])
            .ShouldBe(["Isekai Goumon Hime", "Torture Princess: Fremd Torturchen"]);
    }

    [Fact]
    public void Alternatives_ACandidateRepeatingTheSeriesOwnTitle_IsNotStoredTwice()
    {
        // AniList's romaji and English titles are the same string for plenty of series, and
        // storing it as an alternative would spend a request re-asking the question the first
        // attempt already answered.
        SeriesKnownTitles.Alternatives("Overlord", ["Overlord", "OVERLORD"]).ShouldBeEmpty();
    }

    [Fact]
    public void Alternatives_MoreTitlesThanAreWorthAsking_AreCappedAtEight()
    {
        var candidates = Enumerable.Range(1, 20).Select(number => $"Some Other Name {number}");

        // Each entry is a store request the enrichment may spend, so the list is bounded
        // rather than faithful.
        SeriesKnownTitles.Alternatives("A Series", candidates)
            .Length.ShouldBe(SeriesKnownTitles.MaximumAlternatives);
    }

    [Fact]
    public void Askable_TheAttemptListForARememberedTitle_TriesItOnceAndFirst()
    {
        var attempts = SeriesKnownTitles.Askable(
        [
            "Konosuba - God's Blessing on This Wonderful World!",
            "Kono Subarashii Sekai ni Shukufuku o!",
            "Konosuba - God's Blessing on This Wonderful World!",
            "この素晴らしい世界に祝福を!",
        ]);

        // The remembered title leads so the ordinary night costs one request, and its repeat
        // further down the list is dropped rather than paid for twice.
        attempts.ShouldBe(
        [
            "Konosuba - God's Blessing on This Wonderful World!",
            "Kono Subarashii Sekai ni Shukufuku o!",
        ]);
    }

    [Fact]
    public void Askable_NoRememberedTitle_StillLeadsWithTheShelfsOwn()
    {
        // The null the column holds before anything has been learned drops out silently, so
        // the caller can build one list for both cases.
        SeriesKnownTitles.Askable([null, "86―EIGHTY-SIX", "86--EIGHTY-SIX Alter"])
            .ShouldBe(["86―EIGHTY-SIX", "86--EIGHTY-SIX Alter"]);
    }
}
