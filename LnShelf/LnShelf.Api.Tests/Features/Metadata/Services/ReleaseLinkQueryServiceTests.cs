using static LnShelf.Api.Tests.Features.Metadata.Services.SourceChoiceTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The search behind linking by hand (D8): what is searched, and that a record another series
/// holds is marked rather than offered as free.
/// </summary>
public sealed class ReleaseLinkQueryServiceTests
{
    private const long FreeRecord = 17992364412;
    private const long HeldRecord = 24348008396;

    private readonly IReleaseSource _releaseSource = Substitute.For<IReleaseSource>();

    public ReleaseLinkQueryServiceTests()
    {
        _releaseSource.SourceName.Returns("MangaUpdates");
        _releaseSource.SearchAsync(Arg.Any<string>(), Arg.Any<Format>(), Arg.Any<CancellationToken>())
            .Returns(
            [
                new ReleaseSeriesCandidate { ExternalId = FreeRecord, Title = "86", SourceType = "Novel", StartYear = 2017 },
                new ReleaseSeriesCandidate { ExternalId = HeldRecord, Title = "Youjo Senki", SourceType = "Novel" },
            ]);
    }

    // The matcher's own starting point: MangaUpdates files light novels under romaji.
    [Theory]
    [InlineData(null, "Eighty Six")]
    [InlineData("   ", "Eighty Six")]
    [InlineData(" 86 EIGHTY-SIX ", "86 EIGHTY-SIX")]
    public async Task GetCandidatesAsync_AQuery_SearchesItOrElseTheRomajiTitle(string? query, string expected)
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveUnlinkedSeriesAsync(dbContext);

        var result = await CreateService(dbContext).GetCandidatesAsync(series.Id, query, CancellationToken.None);

        result.ShouldNotBeNull().Query.ShouldBe(expected);
        await _releaseSource.Received(1).SearchAsync(expected, Format.LightNovel, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCandidatesAsync_ARecordAnotherSeriesHolds_IsMarkedWithThatSeries()
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveUnlinkedSeriesAsync(dbContext);
        var holder = CreateSeries();
        holder.AniListId = AniListId + 1;
        holder.TitleEnglish = "The Saga of Tanya the Evil";
        holder.MangaUpdatesId = HeldRecord;
        await SaveSeriesAsync(dbContext, holder);

        var result = await CreateService(dbContext).GetCandidatesAsync(series.Id, query: null, CancellationToken.None);

        var candidates = result.ShouldNotBeNull().Candidates;
        candidates.Count.ShouldBe(2);

        var free = candidates.Single(candidate => candidate.MangaUpdatesId == FreeRecord);
        free.LinkedSeriesId.ShouldBeNull();
        free.SourceType.ShouldBe("Novel");
        free.StartYear.ShouldBe(2017);

        var held = candidates.Single(candidate => candidate.MangaUpdatesId == HeldRecord);
        held.LinkedSeriesId.ShouldBe(holder.Id);
        held.LinkedSeriesTitle.ShouldBe("The Saga of Tanya the Evil");
    }

    [Fact]
    public async Task GetCandidatesAsync_AnUnknownSeries_ReturnsNullWithoutSearching()
    {
        await using var dbContext = CreateDbContext();

        var result = await CreateService(dbContext).GetCandidatesAsync(Guid.CreateVersion7(), "86", CancellationToken.None);

        result.ShouldBeNull();
        await _releaseSource.DidNotReceiveWithAnyArgs().SearchAsync(default!, default, default);
    }

    [Fact]
    public async Task GetCandidatesAsync_AnOverlongQuery_IsRefused()
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveUnlinkedSeriesAsync(dbContext);

        await Should.ThrowAsync<InvalidInputException>(() => CreateService(dbContext).GetCandidatesAsync(
            series.Id, new string('a', ReleaseLinkQueryService.MaximumQueryLength + 1), CancellationToken.None));
    }

    // An HttpClient timeout is a cancelled task nobody asked for: the source not answering.
    [Theory]
    [InlineData("unreachable")]
    [InlineData("timed out")]
    [InlineData("timeout")]
    public async Task GetCandidatesAsync_TheReleaseSourceIsDown_SaysSoAsA503(string failure)
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveUnlinkedSeriesAsync(dbContext);
        _releaseSource.SearchAsync(Arg.Any<string>(), Arg.Any<Format>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<ReleaseSeriesCandidate>>(_ => throw SourceFailure(failure));

        await Should.ThrowAsync<MetadataSourceUnavailableException>(() =>
            CreateService(dbContext).GetCandidatesAsync(series.Id, "86", CancellationToken.None));
    }

    [Fact]
    public async Task GetCandidatesAsync_TheCallerCancels_IsNotAnOutage()
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveUnlinkedSeriesAsync(dbContext);
        using var cancellation = new CancellationTokenSource();
        _releaseSource.SearchAsync(Arg.Any<string>(), Arg.Any<Format>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<ReleaseSeriesCandidate>>(_ =>
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            });

        await Should.ThrowAsync<OperationCanceledException>(() =>
            CreateService(dbContext).GetCandidatesAsync(series.Id, "86", cancellation.Token));
    }

    internal static Exception SourceFailure(string failure) => failure switch
    {
        "timed out" => new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout."),
        "timeout" => new TimeoutException(),
        _ => new HttpRequestException(failure),
    };

    private static async Task<Series> SaveUnlinkedSeriesAsync(LnShelfDbContext dbContext)
    {
        var series = CreateSeries();
        series.MangaUpdatesId = null;
        series.TitleRomaji = "Eighty Six";

        return await SaveSeriesAsync(dbContext, series);
    }

    private ReleaseLinkQueryService CreateService(LnShelfDbContext dbContext) => new(dbContext, _releaseSource);
}
