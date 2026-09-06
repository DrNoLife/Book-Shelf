using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The outbound politeness every metadata request goes through: wait for a permit, honour a
/// 429 once, and never hammer somebody else's free API (CLAUDE.md).
/// </summary>
// Written before the four copy-pasted handlers were consolidated into one, so that the
// consolidation had something to be checked against: every assertion here held for
// AniListRateLimitHandler and OpenLibraryRateLimitHandler first, and holds for the single
// PoliteRateLimitHandler now.
//
// The retry ceiling is set to milliseconds throughout. These tests are about *what* the
// handler does, and a real 60-second back-off would make the suite unrunnable.
public sealed class PoliteRateLimitHandlerTests
{
    [Fact]
    public async Task SendAsync_ASuccessfulResponse_PassesStraightThrough()
    {
        using var upstream = new StubHandler(HttpStatusCode.OK);
        using var client = CreateClient(upstream);

        using var response = await client.GetAsync(new Uri("http://source/series"), CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        upstream.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_A429ThenASuccess_BacksOffAndRetriesOnce()
    {
        using var upstream = new StubHandler(HttpStatusCode.TooManyRequests, HttpStatusCode.OK);
        using var client = CreateClient(upstream);

        using var response = await client.GetAsync(new Uri("http://source/series"), CancellationToken.None);

        // The retry is what makes a 429 survivable at all: the second attempt is the answer
        // the caller gets.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        upstream.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task SendAsync_A429Twice_StopsAndHandsBackTheSecond()
    {
        using var upstream = new StubHandler(HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests);
        using var client = CreateClient(upstream);

        using var response = await client.GetAsync(new Uri("http://source/series"), CancellationToken.None);

        // One retry only. A second 429 means the budget is genuinely gone, and a request that
        // fails loudly beats a queue that grows silently.
        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        upstream.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task SendAsync_AnAdvertisedRetryAfter_IsWhatItWaitsFor()
    {
        using var upstream = new StubHandler(HttpStatusCode.TooManyRequests, HttpStatusCode.OK)
        {
            RetryAfter = "1",
        };

        using var client = CreateClient(upstream, maximumRetryDelay: TimeSpan.FromSeconds(30));

        var stopwatch = Stopwatch.StartNew();
        using var response = await client.GetAsync(new Uri("http://source/series"), CancellationToken.None);
        stopwatch.Stop();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // The source said one second, so one second is what it got — not the ceiling, and not
        // an immediate retry.
        stopwatch.Elapsed.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(800));
    }

    [Fact]
    public async Task SendAsync_APathologicalRetryAfter_IsCappedByTheConfiguredCeiling()
    {
        using var upstream = new StubHandler(HttpStatusCode.TooManyRequests, HttpStatusCode.OK)
        {
            RetryAfter = "3600",
        };

        using var client = CreateClient(upstream, maximumRetryDelay: TimeSpan.FromMilliseconds(50));

        var stopwatch = Stopwatch.StartNew();
        using var response = await client.GetAsync(new Uri("http://source/series"), CancellationToken.None);
        stopwatch.Stop();

        // A source that asks for an hour must not park the shelf for an hour.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task SendAsync_AnAlreadyCancelledToken_NeverReachesTheSource()
    {
        using var upstream = new StubHandler(HttpStatusCode.OK);
        using var client = CreateClient(upstream);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => client.GetAsync(new Uri("http://source/series"), cancellation.Token));

        upstream.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task SendAsync_CancelledWhileBackingOff_StopsRatherThanRetrying()
    {
        using var upstream = new StubHandler(HttpStatusCode.TooManyRequests, HttpStatusCode.OK)
        {
            RetryAfter = "30",
        };

        using var client = CreateClient(upstream, maximumRetryDelay: TimeSpan.FromSeconds(30));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Should.ThrowAsync<OperationCanceledException>(
            () => client.GetAsync(new Uri("http://source/series"), cancellation.Token));

        // The back-off is cancellable, so a shutdown does not have to wait out a 30-second
        // wait somebody else's API asked for.
        upstream.Calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(5, 60, 5)]
    [InlineData(59, 60, 59)]
    [InlineData(3600, 60, 60)]
    [InlineData(60, 60, 60)]
    public void RetryDelayFor_AnAdvertisedDelay_IsHonouredUpToTheCeiling(
        int advertisedSeconds, int maximumSeconds, int expectedSeconds) =>
        PoliteRateLimitHandler.RetryDelayFor(
                new RetryConditionHeaderValue(TimeSpan.FromSeconds(advertisedSeconds)),
                TimeSpan.FromSeconds(maximumSeconds))
            .ShouldBe(TimeSpan.FromSeconds(expectedSeconds));

    [Fact]
    public void RetryDelayFor_NoRetryAfterAtAll_WaitsTheConfiguredCeiling() =>
        // The one rule replacing two. AniList used to wait a hardcoded 60 seconds here and
        // the other three a hardcoded 30; both constants sat beside a per-source ceiling that
        // already meant "the longest this app will wait", so the ceiling is what is used.
        PoliteRateLimitHandler.RetryDelayFor(retryAfter: null, TimeSpan.FromSeconds(45))
            .ShouldBe(TimeSpan.FromSeconds(45));

    [Fact]
    public void RetryDelayFor_ARetryAfterOfZero_WaitsTheConfiguredCeilingRatherThanNothing() =>
        // A source that says "retry immediately" while answering 429 is not to be taken at its
        // word — and AniList's old rule answered this with 60 seconds even when its own
        // ceiling was lower, which was the one case that could escape the configured cap.
        PoliteRateLimitHandler.RetryDelayFor(
                new RetryConditionHeaderValue(TimeSpan.Zero), TimeSpan.FromSeconds(20))
            .ShouldBe(TimeSpan.FromSeconds(20));

    private static HttpClient CreateClient(HttpMessageHandler upstream, TimeSpan? maximumRetryDelay = null)
    {
        var policy = new SourceRateLimitPolicy
        {
            SourceName = "TestSource",
            RequestsPerMinute = 600,
            QueuedRequestLimit = 16,
            MaximumRetryDelay = maximumRetryDelay ?? TimeSpan.FromMilliseconds(20),
        };

        var handler = new PoliteRateLimitHandler(
            new SourceRateLimiter(policy),
            NullLogger<PoliteRateLimitHandler>.Instance)
        {
            InnerHandler = upstream,
        };

        return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    /// <summary>
    /// Answers the given statuses in order, repeating the last one, and counts the attempts.
    /// </summary>
    private sealed class StubHandler(params HttpStatusCode[] statusCodes) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        /// <summary>The <c>Retry-After</c> seconds a 429 advertises, or null for none.</summary>
        public string? RetryAfter { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var status = statusCodes[Math.Min(Calls, statusCodes.Length - 1)];
            Calls++;

            var response = new HttpResponseMessage(status);

            if (status is HttpStatusCode.TooManyRequests && RetryAfter is not null)
            {
                response.Headers.TryAddWithoutValidation("Retry-After", RetryAfter);
            }

            return Task.FromResult(response);
        }
    }
}
