using System.Net;

namespace LnShelf.Client;

/// <summary>
/// Sends the browser to the signed-out landing page when the API says the session is missing
/// or expired; signing in from there brings it back to the page it was on.
/// </summary>
// The whole of the client's part in login (feature-spec §7): the API owns the session
// cookie and the provider flow, so the bundle holds no token and only has to react to 401.
// A full page load: welcome.html is static, outside the app, and its sign-in links start the
// chain of redirects through the provider (js/welcome.js passes the returnUrl on).
public sealed class SignInRedirectHandler(NavigationManager navigation) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized && !IsOnASharedShelf())
        {
            var returnUrl = "/" + navigation.ToBaseRelativePath(navigation.Uri);
            navigation.NavigateTo($"welcome.html?returnUrl={Uri.EscapeDataString(returnUrl)}", forceLoad: true);
        }

        return response;
    }

    // A visitor on a shared shelf has no account here, so a 401 is shown as a failure, not
    // turned into a login. The page only calls anonymous endpoints; this is the seatbelt for
    // a misconfigured policy, where sending a stranger to Google would be the worst outcome.
    private bool IsOnASharedShelf() =>
        navigation.ToBaseRelativePath(navigation.Uri).StartsWith("shared/", StringComparison.Ordinal);
}
