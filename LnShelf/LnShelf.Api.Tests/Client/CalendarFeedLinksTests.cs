using LnShelf.Client.Features.Calendar;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The calendar link points at the API's feed route on the app's own origin, and its webcal
/// form differs only in the scheme.
/// </summary>
public sealed class CalendarFeedLinksTests
{
    private const string Token = "AAAAAAAAAAAAAAAAAAAAAA";

    [Theory]
    [InlineData("https://shelf.barbruh.dev/", "https://shelf.barbruh.dev/api/calendar/AAAAAAAAAAAAAAAAAAAAAA.ics")]
    [InlineData("http://192.168.20.224:8091/", "http://192.168.20.224:8091/api/calendar/AAAAAAAAAAAAAAAAAAAAAA.ics")]
    public void Https_BaseUri_IsTheFeedRouteOnThatOrigin(string baseUri, string expected) =>
        CalendarFeedLinks.Https(baseUri, Token).ShouldBe(expected);

    [Theory]
    [InlineData("https://shelf.barbruh.dev/", "webcal://shelf.barbruh.dev/api/calendar/AAAAAAAAAAAAAAAAAAAAAA.ics")]
    [InlineData("http://192.168.20.224:8091/", "webcal://192.168.20.224:8091/api/calendar/AAAAAAAAAAAAAAAAAAAAAA.ics")]
    public void Webcal_BaseUri_SwapsOnlyTheScheme(string baseUri, string expected) =>
        CalendarFeedLinks.Webcal(baseUri, Token).ShouldBe(expected);
}
