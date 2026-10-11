namespace LnShelf.Api.Features.Auth.Services;

/// <inheritdoc />
// A client of its own (registered in AuthFeatureExtensions) with a short timeout and a response
// buffer capped at AvatarOptions.MaximumFetchBytes: HttpClient refuses a larger body while
// reading it, whether or not the source declared its length.
public sealed class ProviderAvatarFetcher(
    HttpClient httpClient,
    CoverNormalizer normalizer,
    IOptions<AvatarOptions> options,
    ILogger<ProviderAvatarFetcher> logger) : IProviderAvatarFetcher
{
    public async Task<byte[]?> FetchAsync(Uri pictureUri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pictureUri);

        // HTTPS only, so nothing on the way can swap the picture, and nothing but a web address
        // (no file: or internal scheme) is ever fetched from a value that came in a token.
        if (!pictureUri.IsAbsoluteUri || pictureUri.Scheme != Uri.UriSchemeHttps)
        {
            logger.LogWarning("Refused the provider picture at {PictureUri}: not an https address", pictureUri);
            return null;
        }

        using var response = await httpClient.GetAsync(pictureUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        // An absent type is left to the normalizer, which decides by the bytes; a declared
        // non-image one (an error page served with a 200) is not.
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not null && !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Refused the provider picture: the source sent {MediaType}", mediaType);
            return null;
        }

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        return await normalizer.NormalizeAsync(content, options.Value.MaximumEdge, cancellationToken);
    }
}
