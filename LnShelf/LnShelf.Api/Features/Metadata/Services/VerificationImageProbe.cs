using SixLabors.ImageSharp;

namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Downloads one image URL and proves the bytes really are an image.
/// </summary>
// Its own plain HttpClient rather than CoverFetcher or either ICoverCache, and that is the
// whole reason it exists: those store what they fetch, and the verification command's
// contract is that it writes nowhere. Nothing here touches ICoverStore.
//
// No rate-limit handler either, matching the cover fetcher's registration: cover bytes come
// from CDNs rather than from the metadata APIs, so they must not spend those budgets.
public sealed class VerificationImageProbe(HttpClient httpClient, ILogger<VerificationImageProbe> logger)
{
    /// <summary>
    /// Long enough for a slow CDN, short enough that a hung host does not stall a run.
    /// </summary>
    // A constant rather than an options section: this is a one-shot a person watches, and
    // an IOptions binding for a number nobody would ever set would be ceremony.
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    /// <returns>
    /// What the image measured, or null when it did not arrive or did not decode — which
    /// is a result the report prints, not an error the run dies of.
    /// </returns>
    public async Task<VerifiedImage?> ProbeAsync(Uri sourceUri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceUri);

        try
        {
            using var response = await httpClient.GetAsync(sourceUri, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            using var stream = new MemoryStream(content, writable: false);
            using var image = await Image.LoadAsync(stream, cancellationToken);

            logger.LogInformation(
                "Verified the image at {SourceUri}: {ByteCount} bytes, {Width}x{Height}",
                sourceUri,
                content.Length,
                image.Width,
                image.Height);

            return new VerifiedImage
            {
                ByteCount = content.Length,
                Width = image.Width,
                Height = image.Height,
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Deliberately broad, and reported rather than swallowed: an unfetchable cover
            // is precisely what this command exists to notice, so it becomes a missing tick
            // in the report instead of a stack trace that hides the other twenty results.
            logger.LogWarning(exception, "Could not verify the image at {SourceUri}", sourceUri);
            return null;
        }
    }
}
