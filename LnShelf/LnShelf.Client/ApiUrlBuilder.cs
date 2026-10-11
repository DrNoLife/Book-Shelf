namespace LnShelf.Client;

/// <summary>
/// Builds absolute URLs for API-served bytes — images, which the browser fetches
/// itself rather than through <c>HttpClient</c>.
/// </summary>
// It exists because an <img src> cannot use HttpClient.BaseAddress. In production
// the client and API share one origin and this resolves to a relative-looking
// same-origin URL; in development the WASM dev server is a separate origin, so
// the configured API base is the only thing that works.
public sealed class ApiUrlBuilder(HttpClient httpClient)
{
    public string Cover(string objectKey) =>
        new Uri(httpClient.BaseAddress!, $"api/covers/{objectKey}").ToString();

    /// <summary>
    /// The signed-in user's avatar. The version (CurrentUserResponse.AvatarVersion) changes with
    /// the picture, so a new one shows at once while an unchanged one stays cached.
    /// </summary>
    public string Avatar(string version) =>
        new Uri(httpClient.BaseAddress!, $"api/auth/me/avatar?v={Uri.EscapeDataString(version)}").ToString();

    /// <summary>Series art on a shared shelf, through that shelf's own anonymous route (feature-spec §4.8).</summary>
    public string SharedCover(string token, string objectKey) =>
        new Uri(httpClient.BaseAddress!, $"api/shared/{Uri.EscapeDataString(token)}/covers/{objectKey}").ToString();
}
