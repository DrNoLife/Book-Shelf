using static LnShelf.Api.Tests.Features.Import.Fixtures.SeriesMatcherFixture;

namespace LnShelf.Api.Tests.Features.Import.Services;

/// <summary>
/// Breaking a FormatAmbiguous tie on the export's own credits — the one decision
/// the matcher is allowed to take that the title cannot support.
/// </summary>
// Eight of the eleven groups in the real export went to review purely because a
// light novel and its manga adaptation share a title. The adaptation credits the
// novelist as Story, so the author usually matches both; the artist is what tells
// them apart. Every branch of that rule is here, including the three that must
// still end in review — a tie-break that guesses is worse than one that does not
// fire.
public sealed class AniListSeriesMatcherCreditTieBreakTests
{
    [Fact]
    public async Task MatchAsync_AFormatTieAndTheExportNamesTheArtist_TakesThatCandidate()
    {
        // The real Overlord case: both entries credit Kugane Maruyama, because the
        // adaptation is his story. so-bin drew the novel and Hugin Miyama the manga,
        // and the export says which one the reader owns.
        var matcher = CreateMatcher(
            CreateResult(85976, "Overlord", Format.LightNovel, 18, "Kugane Maruyama", "so-bin"),
            CreateResult(85934, "Overlord", Format.Manga, 19, "Kugane Maruyama", "Hugin Miyama"));

        var match = await matcher.MatchAsync(
            "Overlord", author: "Kugane Maruyama", illustrator: "so-bin", CancellationToken.None);

        match.Status.ShouldBe(ImportGroupStatus.AutoMatched);
        match.Reason.ShouldBe(ImportReviewReason.None);
        match.MatchedAniListId.ShouldBe(85976);

        // Both are still staged, so the user can still change their mind in review.
        match.Candidates.Count.ShouldBe(2);
    }

    [Fact]
    public async Task MatchAsync_AFormatTieAndOnlyTheAuthorSeparatesThem_TakesThatCandidate()
    {
        // Two same-titled series by different authors, neither with an artist on
        // record. The author does separate them here, so it is allowed to.
        var matcher = CreateMatcher(
            CreateResult(1, "Torture Princess", Format.LightNovel, 9, "Keishi Ayasato"),
            CreateResult(2, "Torture Princess", Format.Manga, 3, "Saki Ukai"));

        var match = await matcher.MatchAsync(
            "Torture Princess", author: "Keishi Ayasato", illustrator: null, CancellationToken.None);

        match.Status.ShouldBe(ImportGroupStatus.AutoMatched);
        match.MatchedAniListId.ShouldBe(1);
    }

    [Fact]
    public async Task MatchAsync_AFormatTieAndTheAuthorIsCreditedOnBoth_StillGoesToReview()
    {
        // The reason the illustrator is tried first: a manga adaptation credits the
        // novelist as Story, so the author matches both and settles nothing. Two
        // matches is still a tie.
        var matcher = CreateMatcher(
            CreateResult(85976, "Overlord", Format.LightNovel, 18, "Kugane Maruyama", "so-bin"),
            CreateResult(85934, "Overlord", Format.Manga, 19, "Kugane Maruyama", "Hugin Miyama"));

        var match = await matcher.MatchAsync(
            "Overlord", author: "Kugane Maruyama", illustrator: null, CancellationToken.None);

        match.Status.ShouldBe(ImportGroupStatus.NeedsReview);
        match.Reason.ShouldBe(ImportReviewReason.FormatAmbiguous);
        match.MatchedAniListId.ShouldBeNull();
    }

    [Fact]
    public async Task MatchAsync_AFormatTieAndTheArtistMatchesNeither_StillGoesToReview()
    {
        var matcher = CreateMatcher(
            CreateResult(85976, "Overlord", Format.LightNovel, 18, "Kugane Maruyama", "so-bin"),
            CreateResult(85934, "Overlord", Format.Manga, 19, "Kugane Maruyama", "Hugin Miyama"));

        var match = await matcher.MatchAsync(
            "Overlord", author: null, illustrator: "Satoshi Shiki", CancellationToken.None);

        match.Status.ShouldBe(ImportGroupStatus.NeedsReview);
        match.Reason.ShouldBe(ImportReviewReason.FormatAmbiguous);
    }

    [Fact]
    public async Task MatchAsync_AFormatTieAndNeitherCandidateNamesItsArtist_StillGoesToReview()
    {
        // A missing credit is not evidence. Two unknowns must not become a match
        // just because they are equally unknown.
        var matcher = CreateMatcher(
            CreateResult(85976, "Overlord", Format.LightNovel, volumeCount: 18),
            CreateResult(85934, "Overlord", Format.Manga, volumeCount: 19));

        var match = await matcher.MatchAsync(
            "Overlord", author: null, illustrator: null, CancellationToken.None);

        match.Status.ShouldBe(ImportGroupStatus.NeedsReview);
        match.Reason.ShouldBe(ImportReviewReason.FormatAmbiguous);
    }

    [Fact]
    public async Task MatchAsync_TwoCandidatesOfOneFormatTiedOnTitle_IsNotBrokenByCredits()
    {
        // AmbiguousCandidates is a different question — two *manga* with the same
        // title are two different works, and an artist who happens to have drawn one
        // of them is not the reader saying which they read. Only the format tie is
        // broken this way.
        var matcher = CreateMatcher(
            CreateResult(1, "Overlord", Format.Manga, 19, "Kugane Maruyama", "Hugin Miyama"),
            CreateResult(2, "Overlord", Format.Manga, 5, "Someone Else", "Another Artist"));

        var match = await matcher.MatchAsync(
            "Overlord", author: "Kugane Maruyama", illustrator: "Hugin Miyama", CancellationToken.None);

        match.Status.ShouldBe(ImportGroupStatus.NeedsReview);
        match.Reason.ShouldBe(ImportReviewReason.AmbiguousCandidates);
    }

    [Fact]
    public async Task MatchAsync_AFormatTieAndTheArtistMatchesAFarBehindCandidate_StillGoesToReview()
    {
        // The spin-off is not what made this ambiguous — it is forty points behind —
        // so it does not get to win the tie its artist happens to fit.
        var options = new ImportOptions { CandidateMinimumScore = 30 };

        var matcher = CreateMatcher(
            options,
            CreateResult(85976, "Overlord", Format.LightNovel, 18, "Kugane Maruyama", "so-bin"),
            CreateResult(85934, "Overlord", Format.Manga, 19, "Kugane Maruyama", "Hugin Miyama"),
            CreateResult(101951, "Overlord: The Undead King Oh!", Format.Manga, 5, "Juami", "Juami"));

        var match = await matcher.MatchAsync(
            "Overlord", author: null, illustrator: "Juami", CancellationToken.None);

        match.Status.ShouldBe(ImportGroupStatus.NeedsReview);
        match.Reason.ShouldBe(ImportReviewReason.FormatAmbiguous);
    }

    [Fact]
    public async Task MatchAsync_AnArtistSpelledADifferentWay_StillBreaksTheTie()
    {
        // StoryGraph writes "Shinobu Shinotsuki"; another source writes
        // "Shinotsuki, Shinobu.". Same person, and the tie-break has to know it.
        var matcher = CreateMatcher(
            CreateResult(1, "The Saga of Tanya the Evil", Format.LightNovel, 13, "Carlo Zen", "Shinotsuki, Shinobu."),
            CreateResult(2, "The Saga of Tanya the Evil", Format.Manga, 27, "Carlo Zen", "Chika Tojo"));

        var match = await matcher.MatchAsync(
            "The Saga of Tanya the Evil",
            author: null,
            illustrator: "Shinobu Shinotsuki",
            CancellationToken.None);

        match.Status.ShouldBe(ImportGroupStatus.AutoMatched);
        match.MatchedAniListId.ShouldBe(1);
    }
}
