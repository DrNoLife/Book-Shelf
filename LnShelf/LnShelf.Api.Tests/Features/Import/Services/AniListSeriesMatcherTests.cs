using static LnShelf.Api.Tests.Features.Import.Fixtures.SeriesMatcherFixture;

namespace LnShelf.Api.Tests.Features.Import.Services;

/// <summary>
/// The auto-match decision, taken on the title alone. The rule that matters most
/// is the one that refuses to decide: a light novel and its manga adaptation carry
/// the same AniList title, and nothing in a book export's title says which was
/// read.
/// </summary>
// The credit tie-break that can rescue some of those is its own class —
// AniListSeriesMatcherCreditTieBreakTests. Everything here passes no credits at
// all, which is the shape of a source that publishes none.
public sealed class AniListSeriesMatcherTests
{
    [Fact]
    public async Task MatchAsync_ALightNovelAndItsMangaTiedOnTitle_GoesToReview()
    {
        // Both real AniList entries for Overlord: same english, same romaji, same
        // native, different format.
        var matcher = CreateMatcher(
            CreateResult(85976, "Overlord", Format.LightNovel, volumeCount: 18),
            CreateResult(85934, "Overlord", Format.Manga, volumeCount: 19));

        var match = await matcher.MatchAsync(
            "Overlord", author: null, illustrator: null, CancellationToken.None);

        match.Status.ShouldBe(ImportGroupStatus.NeedsReview);
        match.Reason.ShouldBe(ImportReviewReason.FormatAmbiguous);
        match.MatchedAniListId.ShouldBeNull();

        // Both are still offered, best first, so the decision is one click.
        match.Candidates.Count.ShouldBe(2);
        match.Candidates.ShouldAllBe(candidate => candidate.Score == 100);
    }

    [Fact]
    public async Task MatchAsync_OneClearWinner_AutoMatches()
    {
        // The real Bookworm case: the light novel's title is exact, and the manga's
        // is so long it does not even clear the candidate floor.
        var matcher = CreateMatcher(
            CreateResult(87383, "Ascendance of a Bookworm: Part 1", Format.LightNovel, volumeCount: 3),
            CreateResult(96775,
                "Ascendance of a Bookworm ~I'll do anything to become a librarian!~ Part 1 If there aren't any books, I'll just have make some!",
                Format.Manga,
                volumeCount: 7));

        var match = await matcher.MatchAsync(
            "Ascendance of a Bookworm: Part 1", author: null, illustrator: null, CancellationToken.None);

        match.Status.ShouldBe(ImportGroupStatus.AutoMatched);
        match.Reason.ShouldBe(ImportReviewReason.None);
        match.MatchedAniListId.ShouldBe(87383);
        match.MatchScore.ShouldBe(100);
    }

    [Fact]
    public async Task MatchAsync_NothingFound_GoesToReviewWithNoCandidate()
    {
        var matcher = CreateMatcher();

        var match = await matcher.MatchAsync(
            "Vesten mod vesten", author: null, illustrator: null, CancellationToken.None);

        match.Status.ShouldBe(ImportGroupStatus.NeedsReview);
        match.Reason.ShouldBe(ImportReviewReason.NoCandidate);
        match.Candidates.ShouldBeEmpty();
        match.MatchScore.ShouldBeNull();
    }

    [Fact]
    public async Task MatchAsync_OneCandidateWorthShowingButNotAccepting_GoesToReviewWithLowConfidence()
    {
        // A real spin-off that is close enough to be worth offering — it shares
        // three of its words — and nowhere near close enough to accept unseen.
        var matcher = CreateMatcher(
            CreateResult(120957, "86—EIGHTY-SIX: Operation High School", Format.Manga, volumeCount: 2));

        var match = await matcher.MatchAsync(
            "86—EIGHTY-SIX", author: null, illustrator: null, CancellationToken.None);

        match.Status.ShouldBe(ImportGroupStatus.NeedsReview);
        match.Reason.ShouldBe(ImportReviewReason.LowConfidence);
        match.MatchedAniListId.ShouldBeNull();

        // Still offered, so the user can reject it rather than wonder what was found.
        match.Candidates.Count.ShouldBe(1);
        match.MatchScore.ShouldBe(match.Candidates[0].Score);
    }

    [Fact]
    public async Task MatchAsync_ACandidateTooWeakToShow_IsDroppedEntirely()
    {
        // "Overlord: The Undead King Oh!" against "Overlord" scores in the thirties:
        // offering it would be noise, so the group reports having found nothing.
        var matcher = CreateMatcher(
            CreateResult(101951, "Overlord: The Undead King Oh!", Format.Manga, volumeCount: 5));

        var match = await matcher.MatchAsync(
            "Overlord", author: null, illustrator: null, CancellationToken.None);

        match.Status.ShouldBe(ImportGroupStatus.NeedsReview);
        match.Reason.ShouldBe(ImportReviewReason.NoCandidate);
        match.Candidates.ShouldBeEmpty();
    }

    [Fact]
    public async Task MatchAsync_CandidatesBelowTheFloor_AreNotOffered()
    {
        var matcher = CreateMatcher(
            CreateResult(1, "Overlord", Format.LightNovel, volumeCount: 18),
            CreateResult(2, "Overlord à la Carte", Format.Manga, volumeCount: 3));

        var match = await matcher.MatchAsync(
            "Overlord", author: null, illustrator: null, CancellationToken.None);

        // Only the real one survives, so it wins by a clear margin.
        match.Candidates.Count.ShouldBe(1);
        match.Status.ShouldBe(ImportGroupStatus.AutoMatched);
    }

    [Fact]
    public async Task MatchAsync_MoreCandidatesThanTheLimit_KeepsTheBest()
    {
        var options = new ImportOptions { CandidatesPerGroup = 2 };
        var matcher = CreateMatcher(
            options,
            CreateResult(1, "Overlord", Format.LightNovel, volumeCount: 18),
            CreateResult(2, "Overlord", Format.Manga, volumeCount: 19),
            CreateResult(3, "Overlord Anthology", Format.Manga, volumeCount: 1));

        var match = await matcher.MatchAsync(
            "Overlord", author: null, illustrator: null, CancellationToken.None);

        match.Candidates.Count.ShouldBe(2);
        match.Candidates.ShouldAllBe(candidate => candidate.Score == 100);
    }

    [Fact]
    public async Task MatchAsync_TiedCandidates_KeepTheSourcesOwnOrder()
    {
        // AniList returns results by popularity; a tie must not be reordered,
        // because reordering a tie is a preference the matcher has no basis for.
        var matcher = CreateMatcher(
            CreateResult(85934, "Overlord", Format.Manga, volumeCount: 19),
            CreateResult(85976, "Overlord", Format.LightNovel, volumeCount: 18));

        var match = await matcher.MatchAsync(
            "Overlord", author: null, illustrator: null, CancellationToken.None);

        match.Candidates[0].Result.AniListId.ShouldBe(85934);
        match.Candidates[1].Result.AniListId.ShouldBe(85976);
    }
}
