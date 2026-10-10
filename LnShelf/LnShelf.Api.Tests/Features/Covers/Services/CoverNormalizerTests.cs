using LnShelf.Api.Features.Covers.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace LnShelf.Api.Tests.Features.Covers.Services;

/// <summary>
/// What ends up in the object store: the size cap, the re-encode, the metadata strip, and
/// what happens to bytes that are not an image at all (feature-spec §4.3).
/// </summary>
// Worth testing under testing.md's "where bugs are silent and data is at stake": this runs
// once per cover, on ingest, and what it writes is what the shelf renders forever. A cover
// stored at the wrong aspect ratio is not something a later sync repairs.
//
// Images are generated in-memory with the same library the subject uses rather than kept as
// binary fixtures — the inputs here are shapes and sizes, and a checked-in .png would say
// less about the case than two numbers do. The store the normalised bytes go to is tested on
// its own (FileSystemCoverStoreTests).
public sealed class CoverNormalizerTests
{
    [Fact]
    public async Task NormalizeAsync_AnImageWithinTheCap_KeepsItsDimensions()
    {
        var normalized = await Normalizer().NormalizeAsync(Png(600, 900), CancellationToken.None);

        var info = Image.Identify(normalized.ShouldNotBeNull());
        info.Width.ShouldBe(600);
        info.Height.ShouldBe(900);
    }

    [Fact]
    public async Task NormalizeAsync_AnImageSmallerThanTheCap_IsNotScaledUp()
    {
        // A thumbnail the source happened to serve. Upscaling would invent detail and cost
        // bytes for it; the grid scales what it is given.
        var normalized = await Normalizer().NormalizeAsync(Png(40, 60), CancellationToken.None);

        var info = Image.Identify(normalized.ShouldNotBeNull());
        info.Width.ShouldBe(40);
        info.Height.ShouldBe(60);
    }

    [Fact]
    public async Task NormalizeAsync_ATallImageOverTheCap_CapsTheLongEdgeAndKeepsTheAspectRatio()
    {
        // The ordinary case: book covers are portrait, and 2:3 is the shape the tile assumes.
        var normalized = await Normalizer().NormalizeAsync(Png(2000, 3000), CancellationToken.None);

        var info = Image.Identify(normalized.ShouldNotBeNull());
        info.Height.ShouldBe(1000);

        // Max, not Crop (§4.3): cropping a cover to a square cuts the title off, so the
        // short edge lands wherever the ratio puts it.
        info.Width.ShouldBeInRange(666, 667);
    }

    [Fact]
    public async Task NormalizeAsync_AWideImageOverTheCap_CapsTheLongEdgeToo()
    {
        // A wraparound jacket scan, which the drop target accepts as readily as a front cover.
        var normalized = await Normalizer().NormalizeAsync(Png(1200, 800), CancellationToken.None);

        var info = Image.Identify(normalized.ShouldNotBeNull());
        info.Width.ShouldBe(1000);
        info.Height.ShouldBeInRange(666, 667);
    }

    [Fact]
    public async Task NormalizeAsync_AnImageExactlyAtTheCap_IsLeftAlone()
    {
        // The boundary is `>`, not `>=`: an image already at the cap has nothing to gain
        // from a resample and would only lose sharpness to one.
        var normalized = await Normalizer(maximumCoverEdge: 300).NormalizeAsync(
            Png(300, 200), CancellationToken.None);

        var info = Image.Identify(normalized.ShouldNotBeNull());
        info.Width.ShouldBe(300);
        info.Height.ShouldBe(200);
    }

    [Fact]
    public async Task NormalizeAsync_ASmallerCap_IsTheOneThatIsApplied()
    {
        // The cap is configuration, not a constant, and nothing else proves it is read.
        var normalized = await Normalizer(maximumCoverEdge: 300).NormalizeAsync(
            Png(600, 900), CancellationToken.None);

        Image.Identify(normalized.ShouldNotBeNull()).Height.ShouldBe(300);
    }

    [Fact]
    public async Task NormalizeAsync_APng_ComesBackAsWebp()
    {
        var normalized = await Normalizer().NormalizeAsync(Png(200, 300), CancellationToken.None);

        // One stored format, whatever arrived: the cover route serves a single content type
        // (CoverNormalizer.ContentType) and the key carries a single extension.
        Image.DetectFormat(normalized.ShouldNotBeNull()).ShouldBe(WebpFormat.Instance);
    }

    [Fact]
    public async Task NormalizeAsync_LowerQuality_ProducesSmallerFile()
    {
        var source = Png(400, 600);

        var high = await Normalizer(coverQuality: 95).NormalizeAsync(source, CancellationToken.None);
        var low = await Normalizer(coverQuality: 20).NormalizeAsync(source, CancellationToken.None);

        // The encoder has to opt into FileFormat = WebpFileFormatType.Lossy for Quality to be
        // a visual dial at all — ImageSharp's WebpEncoder otherwise defaults to lossless,
        // where Quality only tunes compression effort and barely moves the output (measured
        // against ImageSharp 3.1.12: well under 1% between quality 20 and 95). With the fix,
        // a materially lower quality produces a materially smaller file, and both land well
        // under the raw RGB size the source would take up unencoded.
        Image.Identify(high.ShouldNotBeNull()).Metadata.GetWebpMetadata()
            .FileFormat.ShouldBe(WebpFileFormatType.Lossy);
        low.ShouldNotBeNull();

        low.Length.ShouldBeLessThan((int)(high.Length * 0.7));

        var rawRgbSize = 400 * 600 * 3;
        high.Length.ShouldBeLessThan(rawRgbSize / 2);
        low.Length.ShouldBeLessThan(rawRgbSize / 2);
    }

    [Fact]
    public async Task NormalizeAsync_AnImageCarryingMetadata_StripsIt()
    {
        var source = Png(200, 300, withExif: true);

        // The control: the profile really is on the way in, so the assertion below is about
        // this pass removing it rather than about the encoder never having carried it.
        Image.Identify(source).Metadata.ExifProfile.ShouldNotBeNull();

        var normalized = await Normalizer().NormalizeAsync(source, CancellationToken.None);

        using var stored = Image.Load(normalized.ShouldNotBeNull());
        stored.Metadata.ExifProfile.ShouldBeNull();
        stored.Metadata.IptcProfile.ShouldBeNull();
        stored.Metadata.XmpProfile.ShouldBeNull();
    }

    [Fact]
    public async Task NormalizeAsync_TheSameBytesTwice_ProducesTheSameFile()
    {
        var source = Png(200, 300);
        var normalizer = Normalizer();

        var first = await normalizer.NormalizeAsync(source, CancellationToken.None);
        var second = await normalizer.NormalizeAsync(source, CancellationToken.None);

        // Not decoration: re-running a fetch has to be able to overwrite a stored cover with
        // an identical object, which is what makes the cover cache idempotent.
        second.ShouldBe(first);
    }

    [Theory]
    [InlineData("not an image at all, just a sentence")]
    [InlineData("")]
    public async Task NormalizeAsync_BytesThatAreNotAnImage_ReturnsNull(string content)
    {
        var normalized = await Normalizer().NormalizeAsync(
            Encoding.UTF8.GetBytes(content), CancellationToken.None);

        // Null rather than an exception: an upload that is not an image is a user mistake on
        // a drop target, and the caller answers it with a 400 rather than a 500.
        normalized.ShouldBeNull();
    }

    // Only the formats a cover arrives in are decoded (CoverImageDecoding). TIFF is the one
    // that matters: the ImageSharp line in use has advisories against its TIFF decoder.
    [Fact]
    public async Task NormalizeAsync_ATiff_IsRefused()
    {
        using var image = new Image<Rgba32>(20, 30);
        using var tiff = new MemoryStream();
        await image.SaveAsync(tiff, new SixLabors.ImageSharp.Formats.Tiff.TiffEncoder());

        var normalized = await Normalizer().NormalizeAsync(tiff.ToArray(), CancellationToken.None);

        normalized.ShouldBeNull();
    }

    [Fact]
    public async Task NormalizeAsync_AnImageThatIsTruncated_ReturnsNull()
    {
        var truncated = Png(200, 300)[..64];

        // A different failure from "not an image": the header decodes and the pixels do not.
        // Both are the caller's problem in the same way, so both come back null.
        var normalized = await Normalizer().NormalizeAsync(truncated, CancellationToken.None);

        normalized.ShouldBeNull();
    }

    [Theory]
    [InlineData(200, 300, false)]
    [InlineData(200, 301, true)]
    public async Task NormalizeAsync_AgainstThePixelCap_RefusesOnlyWhatIsOver(int width, int height, bool refused)
    {
        // The cap is on width x height, read from the header before decoding: a small file
        // can claim a canvas that would take gigabytes to decode. Exactly at the cap is fine.
        var normalized = await Normalizer(maximumDecodedPixels: 200 * 300).NormalizeAsync(
            Png(width, height), CancellationToken.None);

        (normalized is null).ShouldBe(refused);
    }

    [Fact]
    public async Task NormalizeAsync_ACancelledToken_ThrowsRatherThanReturningNull()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Cancellation is not a bad image. Folding it into the null answer would have a
        // shutdown mid-sync look like a shelf full of covers nobody could decode.
        await Should.ThrowAsync<OperationCanceledException>(
            () => Normalizer().NormalizeAsync(Png(200, 300), cancellation.Token));
    }

    private static CoverNormalizer Normalizer(
        int maximumCoverEdge = 1000,
        int coverQuality = 82,
        long maximumDecodedPixels = 40_000_000) =>
        new(
            Options.Create(new CoverImageOptions
            {
                MaximumCoverEdge = maximumCoverEdge,
                CoverQuality = coverQuality,
                MaximumDecodedPixels = maximumDecodedPixels,
            }),
            NullLogger<CoverNormalizer>.Instance);

    /// <summary>
    /// A png of the given size, filled with noise so the encoder has something real to spend
    /// quality on — a gradient compresses to the same 270 bytes at any setting, which makes
    /// the quality assertion vacuous. The generator is a fixed-seed sequence written out
    /// rather than <c>Random</c>, whose seeded output .NET does not promise to keep stable.
    /// </summary>
    private static byte[] Png(int width, int height, bool withExif = false)
    {
        using var image = new Image<Rgba32>(width, height);

        image.ProcessPixelRows(accessor =>
        {
            var state = 12345u;

            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);

                for (var x = 0; x < row.Length; x++)
                {
                    state = (state * 1664525) + 1013904223;
                    row[x] = new Rgba32((byte)(state >> 24), (byte)(state >> 16), (byte)(state >> 8));
                }
            }
        });

        if (withExif)
        {
            var profile = new ExifProfile();
            profile.SetValue(ExifTag.Software, "LnShelf tests");
            image.Metadata.ExifProfile = profile;
        }

        using var stream = new MemoryStream();
        image.SaveAsPng(stream);

        return stream.ToArray();
    }
}
