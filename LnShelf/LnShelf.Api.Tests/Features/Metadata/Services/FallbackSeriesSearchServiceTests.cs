using System.Net;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// When a second source is allowed to answer for the first, and when it is not.
/// </summary>
// This exists because of what happened on 2026-08-15: AniList disabled its entire public
// GraphQL API and the shelf could neither search nor add. The fallback is what fixed that,
// and it is exactly the kind of code that is never exercised in the years between outages —
// so which exceptions trigger it is asserted here rather than discovered the next time.
//
// The negative cases matter more than the positive one. A fallback that fires on a rate
// limit turns one API being asked to slow down into two APIs being hammered instead.
public sealed class FallbackSeriesSearchServiceTests
{
    private const string Query = "torture princess";

    [Fact]
    public async Task SearchAsync_TheUsualSourceAnswering_NeverAsksTheFallback()
    {
        var preferred = SourceReturning(AniListHit());
        var fallback = Substitute.For<IMangaUpdatesSeriesQueryService>();

        var outcome = await CreateService(preferred, fallback)
            .SearchAsync(Query, Format.LightNovel, CancellationToken.None);

        outcome.Source.ShouldBe(MetadataSourceName.AniList);
        outcome.Results.Count.ShouldBe(1);

        // Nothing to say, so nothing is said: the page prints no note on a normal search.
        outcome.DegradedNote.ShouldBeNull();

        // And MangaUpdates' budget was not spent on a search AniList already answered.
        await fallback.DidNotReceiveWithAnyArgs()
            .SearchAsync(default!, default, default);
    }

    [Fact]
    public async Task SearchAsync_TheUsualSourceUnavailable_AnswersFromTheFallbackAndSaysSo()
    {
        var preferred = SourceThrowing(Unavailable());
        var fallback = FallbackReturning(MangaUpdatesHit());

        var outcome = await CreateService(preferred, fallback)
            .SearchAsync(Query, Format.LightNovel, CancellationToken.None);

        outcome.Source.ShouldBe(MetadataSourceName.MangaUpdates);
        outcome.Results.ShouldHaveSingleItem().SourceId.ShouldBe(17992364412);

        // Stated, and stated once. Results that silently changed source would explain
        // neither the romaji titles nor the missing volume counts the reader is about to see.
        outcome.DegradedNote.ShouldNotBeNull().ShouldBe(FallbackSeriesSearchService.DegradedNote);
        outcome.DegradedNote.ShouldContain("AniList is unavailable");
        outcome.DegradedNote.ShouldContain("MangaUpdates");
    }

    [Fact]
    public async Task SearchAsync_BothSourcesRefusing_ThrowsTheTypedErrorNamingBoth()
    {
        var fallback = Substitute.For<IMangaUpdatesSeriesQueryService>();
        fallback.SearchAsync(Query, Format.LightNovel, Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<SeriesSearchResult>>>(_ =>
                throw new HttpRequestException("MangaUpdates is down too"));

        var act = () => CreateService(SourceThrowing(Unavailable()), fallback)
            .SearchAsync(Query, Format.LightNovel, CancellationToken.None);

        var thrown = await act.ShouldThrowAsync<MetadataSourceUnavailableException>();

        // Named for the search rather than for one source: with both refusing, "AniList is
        // unavailable" would be half the story, and the reader's situation is that searching
        // does not work at all. The handler turns this into a 503.
        thrown.SourceName.ShouldBe("Series search");

        // AniList's own words are meant for a reader and travel; MangaUpdates' exception
        // message is not, so it is replaced by this app's sentence.
        thrown.UpstreamMessage.ShouldContain("The AniList API has been temporarily disabled");
        thrown.UpstreamMessage.ShouldContain($"MangaUpdates: {ReleaseSourceUnavailable.DidNotAnswer}");
    }

    [Fact]
    public async Task SearchAsync_BothSourcesFailing_KeepsRawExceptionMessagesOffTheWire()
    {
        // A DNS failure on both: the handler sends UpstreamMessage verbatim, and a host name
        // or a deadline is for the log, not the browser.
        var fallback = Substitute.For<IMangaUpdatesSeriesQueryService>();
        fallback.SearchAsync(Query, Format.LightNovel, Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<SeriesSearchResult>>>(_ =>
                throw new HttpRequestException("No such host is known. (api.mangaupdates.com:443)"));

        var act = () => CreateService(
                SourceThrowing(new TimeoutException("AniList did not answer within 20s.")), fallback)
            .SearchAsync(Query, Format.LightNovel, CancellationToken.None);

        var thrown = await act.ShouldThrowAsync<MetadataSourceUnavailableException>();

        thrown.UpstreamMessage.ShouldNotContain("api.mangaupdates.com");
        thrown.UpstreamMessage.ShouldNotContain("No such host");
        thrown.UpstreamMessage.ShouldNotContain("20s");
        thrown.UpstreamMessage.ShouldBe(
            $"Neither AniList nor MangaUpdates could answer. AniList: {ReleaseSourceUnavailable.DidNotAnswer} "
            + $"MangaUpdates: {ReleaseSourceUnavailable.DidNotAnswer}");
    }

    [Fact]
    public async Task SearchAsync_AniListDownAndMangaUpdatesThrottling_SaysBusyAndAnswersWithRetryAfter()
    {
        var fallback = Substitute.For<IMangaUpdatesSeriesQueryService>();
        fallback.SearchAsync(Query, Format.LightNovel, Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<SeriesSearchResult>>>(_ =>
                throw new HttpRequestException(
                    "The MangaUpdates request queue is full; no permit could be acquired.",
                    null,
                    HttpStatusCode.TooManyRequests));

        var act = () => CreateService(SourceThrowing(Unavailable()), fallback)
            .SearchAsync(Query, Format.LightNovel, CancellationToken.None);

        var thrown = await act.ShouldThrowAsync<MetadataSourceUnavailableException>();

        thrown.UpstreamMessage.ShouldContain($"MangaUpdates: {ReleaseSourceUnavailable.AskingToSlowDown}");
        thrown.UpstreamMessage.ShouldNotContain("queue");

        // The wait is known (the throttle's one-minute window), so the caller is told it.
        ProblemDetailsExceptionHandler.RetryAfter(thrown).ShouldBe(TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// The failures a second source is allowed to answer for: the source did not answer.
    /// </summary>
    public static TheoryData<Exception> FailuresWorthAFallback =>
    [
        // The source said, in words, that it is not serving. AniList's real 403 body.
        Unavailable(),

        // HttpClient.Timeout, which surfaces as a cancelled task nobody asked to cancel.
        new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout"),

        new TimeoutException("no answer"),

        // No status at all: DNS, a refused connection, a TLS failure. No exchange happened.
        new HttpRequestException("Name or service not known"),
    ];

    [Theory]
    [MemberData(nameof(FailuresWorthAFallback))]
    public async Task SearchAsync_ASourceThatDidNotAnswer_IsAnsweredForByTheFallback(Exception failure)
    {
        var fallback = FallbackReturning(MangaUpdatesHit());

        var outcome = await CreateService(SourceThrowing(failure), fallback)
            .SearchAsync(Query, Format.LightNovel, CancellationToken.None);

        outcome.Source.ShouldBe(MetadataSourceName.MangaUpdates);
    }

    /// <summary>
    /// The failures that must bubble instead: the source answered, and about us.
    /// </summary>
    public static TheoryData<Exception> FailuresNotWorthAFallback =>
    [
        // Throttling. Waiting is the correct response and the rate-limit handler has already
        // done it once; moving the request onto another free community API would be
        // impoliteness with extra steps.
        new HttpRequestException(
            "Too many requests", null, HttpStatusCode.TooManyRequests),

        // Our own limiter refusing a permit — machinery, not an outage. Thrown as the 429 it
        // amounts to, so it shares the throttling rule above.
        new HttpRequestException(
            "The AniList request queue is full; no permit could be acquired.", null, HttpStatusCode.TooManyRequests),

        // A GraphQL-level rejection on a 200: the query is wrong, which is this app's defect.
        new InvalidOperationException("AniList rejected SeriesSearch: Unknown field 'volumez'"),
    ];

    [Theory]
    [MemberData(nameof(FailuresNotWorthAFallback))]
    public async Task SearchAsync_ASourceAnsweringAboutUs_BubblesAndSparesTheFallback(Exception failure)
    {
        var fallback = Substitute.For<IMangaUpdatesSeriesQueryService>();

        var act = () => CreateService(SourceThrowing(failure), fallback)
            .SearchAsync(Query, Format.LightNovel, CancellationToken.None);

        (await act.ShouldThrowAsync<Exception>()).ShouldBeSameAs(failure);

        await fallback.DidNotReceiveWithAnyArgs().SearchAsync(default!, default, default);
    }

    [Fact]
    public async Task SearchAsync_ACancelledRequest_IsNobodysOutage()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var fallback = Substitute.For<IMangaUpdatesSeriesQueryService>();
        var preferred = SourceThrowing(new OperationCanceledException(cancellation.Token));

        var act = () => CreateService(preferred, fallback)
            .SearchAsync(Query, Format.LightNovel, cancellation.Token);

        await act.ShouldThrowAsync<OperationCanceledException>();

        // The browser navigated away. Proving that with somebody else's API would be rude.
        await fallback.DidNotReceiveWithAnyArgs().SearchAsync(default!, default, default);
    }

    private static FallbackSeriesSearchService CreateService(
        ISeriesMetadataSource preferred,
        IMangaUpdatesSeriesQueryService fallback) =>
        new(preferred, fallback, NullLogger<FallbackSeriesSearchService>.Instance);

    private static MetadataSourceUnavailableException Unavailable() =>
        new(
            nameof(MetadataSourceName.AniList),
            "The AniList API has been temporarily disabled due to severe stability issues.",
            403);

    private static ISeriesMetadataSource SourceReturning(params SeriesSearchResult[] results)
    {
        var source = Substitute.For<ISeriesMetadataSource>();
        source.SearchAsync(Query, Format.LightNovel, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<SeriesSearchResult>>(results);

        return source;
    }

    private static ISeriesMetadataSource SourceThrowing(Exception failure)
    {
        var source = Substitute.For<ISeriesMetadataSource>();
        source.SearchAsync(Query, Format.LightNovel, Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<SeriesSearchResult>>>(_ => throw failure);

        return source;
    }

    private static IMangaUpdatesSeriesQueryService FallbackReturning(params SeriesSearchResult[] results)
    {
        var source = Substitute.For<IMangaUpdatesSeriesQueryService>();
        source.SearchAsync(Query, Format.LightNovel, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<SeriesSearchResult>>(results);

        return source;
    }

    private static SeriesSearchResult AniListHit() => new()
    {
        Source = MetadataSourceName.AniList,
        SourceId = 97100,
        TitleEnglish = "Torture Princess: Fremd Torturchen",
        Format = Format.LightNovel,
        PublicationStatus = PublicationStatus.Completed,
    };

    private static SeriesSearchResult MangaUpdatesHit() => new()
    {
        Source = MetadataSourceName.MangaUpdates,
        SourceId = 17992364412,
        TitleEnglish = "Fremd Torturchen",
        Format = Format.LightNovel,
        PublicationStatus = PublicationStatus.Ongoing,
    };
}
