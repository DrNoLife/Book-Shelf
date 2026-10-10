using System.Net;
using System.Net.Http.Headers;
using LnShelf.Api.Features.Covers.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace LnShelf.Api.Tests.Features.Covers.Services;

/// <summary>
/// What a synced cover has to be before it is stored: an image, under the size cap, and
/// re-encoded as the app's own webp — the same bar a dropped cover clears.
/// </summary>
// The network is a stub handler; the decision under test is what gets stored, not whether a
// CDN answers.
public sealed class CoverFetcherTests
{
    private const string ObjectKey = "anilist/85470.jpg";
    private static readonly Uri SourceUri = new("https://cdn.example.com/cover/85470.jpg");

    [Fact]
    public async Task EnsureCachedAsync_ARealImage_IsStoredAsWebp()
    {
        var store = Substitute.For<ICoverStore>();
        byte[]? stored = null;
        await store.SaveAsync(
            ObjectKey,
            Arg.Do<ReadOnlyMemory<byte>>(content => stored = content.ToArray()),
            CoverNormalizer.ContentType,
            Arg.Any<CancellationToken>());

        var key = await CreateFetcher(store, Respond(Png(), "image/png"))
            .EnsureCachedAsync(ObjectKey, SourceUri, "a test cover", CancellationToken.None);

        key.ShouldBe(ObjectKey);

        // Stored under the webp content type and actually webp, whatever the CDN sent.
        Image.DetectFormat(stored.ShouldNotBeNull()).ShouldBe(WebpFormat.Instance);
    }

    public static TheoryData<string, HttpContent> RefusedResponses => new()
    {
        // An HTML error page served with a 200, which used to be stored and served as a cover.
        { "a declared non-image", Respond(Encoding.UTF8.GetBytes("<html>Not found</html>"), "text/html") },

        // Says it is an image and is not one.
        { "bytes that do not decode", Respond(Encoding.UTF8.GetBytes("definitely not a jpeg"), "image/jpeg") },

        // Over the cap by its own account.
        { "a declared size over the cap", Respond(new byte[MaximumBytes + 1], "image/jpeg") },

        // Over the cap with no Content-Length to say so up front.
        { "an undeclared size over the cap", new UnsizedContent(new byte[MaximumBytes + 1]) },
    };

    [Theory]
    [MemberData(nameof(RefusedResponses))]
    public async Task EnsureCachedAsync_ARefusedResponse_StoresNothingAndReturnsNull(string because, HttpContent content)
    {
        var store = Substitute.For<ICoverStore>();

        var key = await CreateFetcher(store, content)
            .EnsureCachedAsync(ObjectKey, SourceUri, because, CancellationToken.None);

        // Treated as a failed fetch: no cover now, and the next sync asks again.
        key.ShouldBeNull();
        await store.DidNotReceive().SaveAsync(
            Arg.Any<string>(), Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private const int MaximumBytes = 64 * 1024;

    private static CoverFetcher CreateFetcher(ICoverStore store, HttpContent content)
    {
        var options = Options.Create(new CoverStoreOptions { MaximumUploadBytes = MaximumBytes });

        return new CoverFetcher(
            store,
            new CoverNormalizer(options, NullLogger<CoverNormalizer>.Instance),
            new HttpClient(new StubHandler(content)),
            options,
            NullLogger<CoverFetcher>.Instance);
    }

    private static ByteArrayContent Respond(byte[] bytes, string mediaType)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);

        return content;
    }

    private static byte[] Png()
    {
        using var image = new Image<Rgba32>(20, 30);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);

        return stream.ToArray();
    }

    private sealed class StubHandler(HttpContent content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }

    /// <summary>A body that declares no length, as a chunked response does.</summary>
    private sealed class UnsizedContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
