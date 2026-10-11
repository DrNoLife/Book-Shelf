using LnShelf.Api.Features.Calendar.Abstractions;
using LnShelf.Api.Features.Calendar.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LnShelf.Api.Tests.Features.Calendar.Controllers;

/// <summary>
/// The feed's HTTP contract: the content type a calendar app accepts, a cache header that
/// keeps a turned-off link out of shared caches, and one 404 for every token that is not on.
/// </summary>
// A controller test, which testing.md would normally call ceremony. The headers live only in
// the controller, and a refactor to [ResponseCache] or File(...) could quietly drop "private"
// and let the Cloudflare edge keep serving a revoked feed.
public sealed class ReleaseCalendarControllerTests
{
    private const string Token = "AAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    public async Task GetAsync_AFeedThatIsOn_IsTextCalendarAndPrivatelyCachedForAnHour()
    {
        var releaseCalendar = Substitute.For<IReleaseCalendarQueryService>();
        releaseCalendar.GetAsync(Token, Arg.Any<CancellationToken>()).Returns("BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n");
        var controller = ControllerFor(releaseCalendar);

        var result = await controller.GetAsync(Token, CancellationToken.None);

        var content = result.ShouldBeOfType<ContentResult>();
        content.ContentType.ShouldBe("text/calendar; charset=utf-8");
        content.Content.ShouldBe("BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n");
        controller.Response.Headers.CacheControl.ToString().ShouldBe("private, max-age=3600");
    }

    [Theory]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("not-a-token")]
    [InlineData("")]
    public async Task GetAsync_UnknownTurnedOffOrMalformedToken_IsTheSame404(string token)
    {
        // The query service answers null for all three; the controller must not tell them apart.
        var releaseCalendar = Substitute.For<IReleaseCalendarQueryService>();
        releaseCalendar.GetAsync(default!, default).ReturnsForAnyArgs((string?)null);
        var controller = ControllerFor(releaseCalendar);

        var result = await controller.GetAsync(token, CancellationToken.None);

        result.ShouldBeOfType<NotFoundResult>();
        controller.Response.Headers.CacheControl.ToString().ShouldBeEmpty();
    }

    private static ReleaseCalendarController ControllerFor(IReleaseCalendarQueryService releaseCalendar) =>
        new(releaseCalendar)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
}
