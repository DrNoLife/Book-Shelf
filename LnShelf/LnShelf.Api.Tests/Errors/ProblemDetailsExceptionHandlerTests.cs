using LnShelf.Api.Tests.Data.Fixtures;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;

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
    public void Describe_AUniqueViolation_Is409AndSaysToReload()
    {
        // Two requests racing to write one row: the index caught it, nothing is broken, and
        // reloading shows the row the other request wrote.
        var (statusCode, _, detail) = ProblemDetailsExceptionHandler.Describe(
            RefusingSaveInterceptor.UniqueViolation());

        statusCode.ShouldBe(409);
        detail.ShouldNotBeNull().ShouldContain("Reload");
    }

    [Fact]
    public void Describe_AnyOtherDatabaseRefusal_StaysA500()
    {
        // A foreign key failing is a defect of ours, not a race, and must not be dressed up
        // as "try again".
        var foreignKey = new Microsoft.EntityFrameworkCore.DbUpdateException(
            "An error occurred while saving the entity changes.",
            new Npgsql.PostgresException("violates foreign key constraint", "ERROR", "ERROR", Npgsql.PostgresErrorCodes.ForeignKeyViolation));

        ProblemDetailsExceptionHandler.Describe(foreignKey).StatusCode.ShouldBe(500);
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

    [Theory]
    [InlineData(413, "Too large")]
    [InlineData(400, "Bad request")]
    public void Describe_KestrelRefusingTheRequest_KeepsItsStatus(int status, string expectedTitle)
    {
        var (statusCode, title, detail) = ProblemDetailsExceptionHandler.Describe(
            new Microsoft.AspNetCore.Http.BadHttpRequestException("Request body too large.", status));

        // Not a 500: an upload over the limit is the client's mistake, and the status says so.
        statusCode.ShouldBe(status);
        title.ShouldBe(expectedTitle);
        detail.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("api/calendar/{token}.ics", "/api/calendar/SecretCalendarToken0001.ics", "/api/calendar/{token}.ics")]
    [InlineData("api/shared/{token}", "/api/shared/SecretSharedToken00000001", "/api/shared/{token}")]
    public async Task TryHandleAsync_ATokenRouteFailing_LogsTheRouteTemplateNotTheToken(
        string routeTemplate, string requestPath, string expectedPath)
    {
        // A Postgres outage inside the anonymous calendar feed: the 500 is logged, and whoever
        // reads the log must not be able to open the feed (or the shared shelf) with it.
        var logger = new CapturingLogger();
        var httpContext = FailedRequest(routeTemplate, requestPath);

        await HandlerLoggingTo(logger).TryHandleAsync(
            httpContext, new InvalidOperationException("connection refused"), CancellationToken.None);

        var message = logger.Messages.ShouldHaveSingleItem();
        message.ShouldContain(expectedPath);
        message.ShouldNotContain("Secret");
    }

    [Fact]
    public async Task TryHandleAsync_AnOrdinaryRouteFailing_LogsTheRealPath()
    {
        // Everything without a secret in it keeps the path that makes a 500 traceable.
        var logger = new CapturingLogger();
        var httpContext = FailedRequest("api/series/{id}", "/api/series/0199a0f1-1111-7000-8000-000000000001");

        await HandlerLoggingTo(logger).TryHandleAsync(
            httpContext, new InvalidOperationException("boom"), CancellationToken.None);

        logger.Messages.ShouldHaveSingleItem().ShouldContain("/api/series/0199a0f1-1111-7000-8000-000000000001");
    }

    private static ProblemDetailsExceptionHandler HandlerLoggingTo(CapturingLogger logger) =>
        new(Substitute.For<IProblemDetailsService>(), logger);

    // As the exception middleware leaves it: the endpoint cleared from the context, the
    // matched one kept on the feature.
    private static DefaultHttpContext FailedRequest(string routeTemplate, string requestPath)
    {
        var endpoint = new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse(routeTemplate),
            order: 0,
            EndpointMetadataCollection.Empty,
            displayName: routeTemplate);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = requestPath;
        httpContext.Features.Set<IExceptionHandlerFeature>(
            new ExceptionHandlerFeature
            {
                Error = new InvalidOperationException(),
                Path = requestPath,
                Endpoint = endpoint,
            });

        return httpContext;
    }

    private sealed class CapturingLogger : ILogger<ProblemDetailsExceptionHandler>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
