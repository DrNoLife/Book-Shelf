namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The chapter-map step of a refresh, for the tests that build SeriesRefresher by hand.
/// </summary>
// Ten test files construct the refresher, and almost none of them are about chapter maps: they
// get the step with no sources, which is a real path (it asks nobody and writes nothing).
internal static class ChapterMapPipeline
{
    public static ChapterMapRefresher NoSources(TimeProvider? timeProvider = null) =>
        new([], timeProvider ?? TimeProvider.System, NullLogger<ChapterMapRefresher>.Instance);
}
