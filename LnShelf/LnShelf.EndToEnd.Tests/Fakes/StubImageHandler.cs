using System.Net;
using System.Net.Http.Headers;

namespace LnShelf.EndToEnd.Tests.Fakes;

/// <summary>
/// The one HTTP-level fake: CoverFetcher downloads absolute CDN URLs taken from the
/// payloads, so there is no seam to swap. Every request gets the same small PNG.
/// </summary>
public sealed class StubImageHandler : HttpMessageHandler
{
    public static string ImagePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "cover-64x64.png");

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(ImagePath, cancellationToken);
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }
}
