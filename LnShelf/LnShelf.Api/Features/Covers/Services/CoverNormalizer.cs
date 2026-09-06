using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace LnShelf.Api.Features.Covers.Services;

/// <summary>
/// Normalization on ingest, per feature-spec §4.3: cap dimensions, re-encode to
/// webp, strip metadata.
/// </summary>
// One size class in v1 (§4.3): the grid renders covers at two scales from the same
// file, and thumbnails are a measured-need optimisation rather than a default.
public sealed class CoverNormalizer(IOptions<CoverStoreOptions> options, ILogger<CoverNormalizer> logger)
{
    public const string ContentType = "image/webp";
    public const string Extension = ".webp";

    /// <returns>The normalized bytes, or null when the input is not a decodable image.</returns>
    public async Task<byte[]?> NormalizeAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = new MemoryStream(source.ToArray(), writable: false);

            // Loading through ImageSharp is also the validation: an upload that is not
            // an image fails here rather than being stored and served as one.
            using var image = await Image.LoadAsync(stream, cancellationToken);

            var maximum = options.Value.MaximumCoverEdge;
            if (image.Width > maximum || image.Height > maximum)
            {
                image.Mutate(context => context.Resize(new ResizeOptions
                {
                    Size = new Size(maximum, maximum),

                    // Max, not Crop: a cover's aspect ratio is part of the artwork, and
                    // cropping one to a square would cut the title off.
                    Mode = ResizeMode.Max,
                }));
            }

            // Everything the camera, scanner or CDN left behind goes.
            image.Metadata.ExifProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;

            using var output = new MemoryStream();

            // ImageSharp's WebpEncoder defaults to lossless, which ignores Quality
            // entirely and stores the image at close to its raw RGB size.
            await image.SaveAsync(
                output,
                new WebpEncoder { FileFormat = WebpFileFormatType.Lossy, Quality = options.Value.CoverQuality },
                cancellationToken);

            var normalized = output.ToArray();

            logger.LogInformation(
                "Normalized a cover: {SourceBytes} bytes in, {NormalizedBytes} bytes out at {Width}x{Height}",
                source.Length, normalized.Length, image.Width, image.Height);

            return normalized;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is UnknownImageFormatException or InvalidImageContentException)
        {
            logger.LogWarning("Refused a cover that is not a decodable image: {Reason}", exception.Message);
            return null;
        }
    }
}
