using LnShelf.Api.Errors;

namespace LnShelf.Api.Tests.Errors;

/// <summary>
/// What one exception becomes on the wire — the whole behaviour of the central handler.
/// </summary>
// Asserted against the mapping rather than through a hosted request, because the mapping is
// the part with a decision in it and the writing is the framework's. The 503 is the reason
// this file exists: an external source refusing to serve used to come back as a 500 with the
// message stripped, and "net_http_message_not_success_statuscode_reason, 500" is what a
// person then read on the search page.
public sealed class ProblemDetailsExceptionHandlerTests
{
    [Fact]
    public void Describe_AMetadataSourceRefusingToServe_Is503WithTheSourcesOwnWords()
    {
        var exception = new MetadataSourceUnavailableException(
            nameof(MetadataSourceName.AniList),
            "The AniList API has been temporarily disabled due to severe stability issues.",
            403);

        var (statusCode, title, detail) = ProblemDetailsExceptionHandler.Describe(exception);

        // 503, not 500: nothing here is broken, and 503 is the status that says "try later".
        statusCode.ShouldBe(503);

        // The title names the source, so the page does not have to guess whose outage it is.
        title.ShouldBe("AniList is unavailable");

        // The detail is the upstream's own sentence, not this app's wrapper around it: it is
        // the only part that tells a reader anything they can act on.
        detail.ShouldBe("The AniList API has been temporarily disabled due to severe stability issues.");
    }

    [Fact]
    public void Describe_TheUpstreamStatus_IsKeptOnTheExceptionForTheLog()
    {
        var exception = new MetadataSourceUnavailableException("MangaUpdates", "gateway timeout", 504);

        exception.UpstreamStatusCode.ShouldBe(504);

        // The wrapper message is what the log line carries, and it says both halves.
        exception.Message.ShouldBe("MangaUpdates is unavailable (504): gateway timeout");
    }

    [Fact]
    public void Describe_InvalidInput_Is400WithTheReason()
    {
        var (statusCode, title, detail) = ProblemDetailsExceptionHandler.Describe(
            new InvalidInputException("Adding a series needs exactly one of aniListId or mangaUpdatesId."));

        statusCode.ShouldBe(400);
        title.ShouldBe("Invalid input");
        detail.ShouldNotBeNull().ShouldContain("exactly one");
    }

    [Fact]
    public void Describe_AConflict_Is409()
    {
        ProblemDetailsExceptionHandler.Describe(new ConflictException("not in this state"))
            .StatusCode.ShouldBe(409);
    }

    [Fact]
    public void Describe_AnythingElse_Is500AndSaysNothing()
    {
        var (statusCode, title, detail) = ProblemDetailsExceptionHandler.Describe(
            new InvalidOperationException("a connection string with the password in it"));

        statusCode.ShouldBe(500);
        title.ShouldBe("Unexpected failure");

        // A 500 could be anything, so the message stays in the log rather than on the wire.
        detail.ShouldBeNull();
    }
}
