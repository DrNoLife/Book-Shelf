using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace LnShelf.Api.Features.Covers.Services;

/// <summary>
/// The image formats a cover may arrive in. Everything else is refused at decode.
/// </summary>
// Narrower than ImageSharp's default on purpose. Covers come from user uploads and from
// CDNs, and none of them are TIFF, BMP, TGA or the other formats the default decodes. The
// 3.1.x line carries published advisories for TIFF decoding (GHSA-wmxv-xphr-5c9g), so
// leaving those decoders out removes them from the attack surface rather than trusting
// that nobody sends one. The audit suppressions in Directory.Build.props rely on this.
public static class CoverImageDecoding
{
    public static DecoderOptions Options { get; } = new()
    {
        Configuration = new Configuration(
            new JpegConfigurationModule(),
            new PngConfigurationModule(),
            new WebpConfigurationModule(),
            new GifConfigurationModule()),
    };
}
