using System.Net;

namespace LnShelf.Client;

/// <summary>
/// Sends the browser to sign in when the API says the session is missing or expired, and
/// brings it back to the page it was on.
/// </summary>
// The whole of the client's part in login (feature-spec §7): the API owns the session
// cookie and the provider flow, so the bundle holds no token and only has to react to 401.
// A full page load, because the login is a chain of redirects through the provider.
public sealed class SignInRedirectHandler(NavigationManager navigation) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized)
        {
            var returnUrl = "/" + navigation.ToBaseRelativePath(navigation.Uri);
            navigation.NavigateTo($"api/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}", forceLoad: true);
        }

        return response;
    }
}
