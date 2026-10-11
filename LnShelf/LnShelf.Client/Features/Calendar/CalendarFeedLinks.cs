namespace LnShelf.Client.Features.Calendar;

/// <summary>
/// The two addresses of a release calendar: the plain one to paste into a calendar app, and
/// the webcal one that opens the calendar app straight away.
/// </summary>
// Built from the client's own base address, as the shared shelf link is, so the API never
// has to know the hostname it is published under. The feed is under /api/ because nginx
// only proxies that prefix to the API.
public static class CalendarFeedLinks
{
    /// <param name="baseUri">NavigationManager.BaseUri: scheme, host and a trailing slash.</param>
    public static string Https(string baseUri, string token) => $"{baseUri}api/calendar/{token}.ics";

    /// <summary>
    /// The same address under webcal://, which Apple Calendar, Outlook and most phones hand
    /// to the calendar app as a subscription. The scheme carries no TLS choice of its own; the
    /// app fetches over https where the site has it.
    /// </summary>
    public static string Webcal(string baseUri, string token)
    {
        var address = Https(baseUri, token);
        var schemeEnd = address.IndexOf("://", StringComparison.Ordinal);

        return schemeEnd < 0 ? address : $"webcal{address[schemeEnd..]}";
    }
}
