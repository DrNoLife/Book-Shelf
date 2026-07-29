namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// Linking a shelf series to the release source, and — more importantly — the four
/// ways it declines to.
/// </summary>
public sealed class ReleaseSourceMatcherTests
{
    private const long YoujoSenkiId = 24348008396;
    private const long BookwormId = 37107680441;

    [Fact]
    public async Task MatchAsync_AnExactAssociatedTitle_Links()
    {
        // The real Tanya case: MangaUpdates' primary title is the romaji one, and the
        // shelf's English name is only in the associated list.
        var matcher = CreateMatcher(
            (YoujoSenkiId, "Youjo Senki", ["The Saga of Tanya the Evil"], 14));

        var match = await matcher.MatchAsync(
            CreateSeries("The Saga of Tanya the Evil", "Youjo Senki"), NothingLinked, CancellationToken.None);

        match.Outcome.ShouldBe(ReleaseMatchOutcome.Linked);
        match.MatchedExternalId.ShouldBe(YoujoSenkiId);
        match.Score.ShouldBe(100);
        match.MatchedSeries.ShouldNotBeNull().VolumeCount.ShouldBe(14);
    }

    [Fact]
    public async Task MatchAsync_ATitleOnlyTheAssociatedListHas_StillLinks()
    {
        // The real 86 case: MangaUpdates files it under the primary title "86",
        // which scores 50 against the shelf's "86―EIGHTY-SIX". Only the associated
        // list saves it.
        var matcher = CreateMatcher(
            (20020870409, "86", ["86--EIGHTY-SIX", "86--EIGHTY-SIX Alter"], 14));

        var match = await matcher.MatchAsync(
            CreateSeries("86―EIGHTY-SIX", "86: Eighty Six"), NothingLinked, CancellationToken.None);

        match.Outcome.ShouldBe(ReleaseMatchOutcome.Linked);
        match.Score.ShouldBe(100);
    }

    [Fact]
    public async Task MatchAsync_TheBestCandidateAlreadyLinkedElsewhere_DeclinesAndSaysWhy()
    {
        // The real Ascendance of a Bookworm case: MangaUpdates has one series of 33
        // volumes where AniList has one series per part, so the shelf's two entries
        // have a single correct answer between them — which neither can hold, because
        // the link is unique.
        var matcher = CreateMatcher(
            (BookwormId, "Ascendance of a Bookworm", ["Honzuki no Gekokujou"], 33));

        var match = await matcher.MatchAsync(
            CreateSeries("Ascendance of a Bookworm", "Honzuki no Gekokujou"),
            new HashSet<long> { BookwormId },
            CancellationToken.None);

        match.Outcome.ShouldBe(ReleaseMatchOutcome.AlreadyLinked);
        match.MatchedExternalId.ShouldBeNull();
        match.Explanation.ShouldContain("already linked");
    }

    [Fact]
    public async Task MatchAsync_NothingFound_DeclinesWithNoCandidate()
    {
        var matcher = CreateMatcher();

        var match = await matcher.MatchAsync(
            CreateSeries("Vesten mod vesten", null), NothingLinked, CancellationToken.None);

        match.Outcome.ShouldBe(ReleaseMatchOutcome.NoCandidate);
        match.MatchedExternalId.ShouldBeNull();
    }

    [Fact]
    public async Task MatchAsync_OnlyADistantTitle_DeclinesWithLowConfidence()
    {
        var matcher = CreateMatcher(
            (99, "Nidome no Yuusha", [], 8));

        var match = await matcher.MatchAsync(
            CreateSeries("The Hero Laughs While Walking the Path of Vengeance a Second Time", null),
            NothingLinked,
            CancellationToken.None);

        match.Outcome.ShouldBe(ReleaseMatchOutcome.LowConfidence);
        match.MatchedExternalId.ShouldBeNull();
        match.Explanation.ShouldContain("Nidome no Yuusha");
    }

    [Fact]
    public async Task MatchAsync_TwoEquallyGoodCandidates_DeclinesAsAmbiguous()
    {
        var matcher = CreateMatcher(
            (1, "Overlord", [], 16),
            (2, "Overlord", [], 19));

        var match = await matcher.MatchAsync(
            CreateSeries("Overlord", "Overlord"), NothingLinked, CancellationToken.None);

        match.Outcome.ShouldBe(ReleaseMatchOutcome.Ambiguous);
        match.MatchedExternalId.ShouldBeNull();
    }

    [Fact]
    public async Task MatchAsync_RomajiFindsNothing_FallsBackToTheEnglishTitle()
    {
        var releaseSource = Substitute.For<IReleaseSource>();
        releaseSource.SourceName.Returns("MangaUpdates");

        // Romaji search is tried first and comes back empty; English finds it.
        releaseSource.SearchAsync("Nothing By This Name", Arg.Any<Format>(), Arg.Any<CancellationToken>())
            .Returns([]);
        releaseSource.SearchAsync("Youjo Senki Alternative", Arg.Any<Format>(), Arg.Any<CancellationToken>())
            .Returns([new ReleaseSeriesCandidate { ExternalId = YoujoSenkiId, Title = "Youjo Senki Alternative" }]);
        releaseSource.GetSeriesAsync(YoujoSenkiId, Arg.Any<CancellationToken>())
            .Returns(new ReleaseSeriesInfo
            {
                ExternalId = YoujoSenkiId,
                Title = "Youjo Senki Alternative",
                VolumeCount = 3,
            });

        var matcher = new ReleaseSourceMatcher(
            releaseSource,
            Options.Create(new MangaUpdatesOptions()),
            NullLogger<ReleaseSourceMatcher>.Instance);

        var match = await matcher.MatchAsync(
            CreateSeries("Youjo Senki Alternative", "Nothing By This Name"),
            NothingLinked,
            CancellationToken.None);

        match.Outcome.ShouldBe(ReleaseMatchOutcome.Linked);
    }

    private static readonly IReadOnlySet<long> NothingLinked = new HashSet<long>();

    private static ReleaseSourceMatcher CreateMatcher(
        params (long ExternalId, string Title, string[] Associated, int? VolumeCount)[] candidates)
    {
        var releaseSource = Substitute.For<IReleaseSource>();
        releaseSource.SourceName.Returns("MangaUpdates");

        releaseSource.SearchAsync(Arg.Any<string>(), Arg.Any<Format>(), Arg.Any<CancellationToken>())
            .Returns([.. candidates.Select(candidate => new ReleaseSeriesCandidate
            {
                ExternalId = candidate.ExternalId,
                Title = candidate.Title,
                SourceType = "Novel",
            })]);

        foreach (var candidate in candidates)
        {
            releaseSource.GetSeriesAsync(candidate.ExternalId, Arg.Any<CancellationToken>())
                .Returns(new ReleaseSeriesInfo
                {
                    ExternalId = candidate.ExternalId,
                    Title = candidate.Title,
                    AssociatedTitles = candidate.Associated,
                    SourceType = "Novel",
                    VolumeCount = candidate.VolumeCount,
                    RawStatus = $"{candidate.VolumeCount} Volumes (Ongoing)",
                });
        }

        return new ReleaseSourceMatcher(
            releaseSource,
            Options.Create(new MangaUpdatesOptions()),
            NullLogger<ReleaseSourceMatcher>.Instance);
    }

    private static Series CreateSeries(string titleEnglish, string? titleRomaji) => new()
    {
        Id = Guid.CreateVersion7(),
        AniListId = 1,
        TitleEnglish = titleEnglish,
        TitleRomaji = titleRomaji,
        Format = Format.LightNovel,
        PublicationStatus = PublicationStatus.Ongoing,
    };
}
