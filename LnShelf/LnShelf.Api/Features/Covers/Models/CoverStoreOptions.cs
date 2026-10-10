using System.ComponentModel.DataAnnotations;

namespace LnShelf.Api.Features.Covers.Models;

/// <summary>
/// MinIO connection settings, bound once through <c>IOptions&lt;T&gt;</c>.
/// </summary>
// The keys are credentials, so in the container they arrive as environment
// variables (feature-spec §9). The values checked into
// appsettings.Development.json are the local compose credentials and nothing
// else — the same standing exception docker-compose.yml already documents.
public sealed class CoverStoreOptions
{
    public const string SectionName = "Covers";

    /// <summary>Host and port, S3 style — no scheme.</summary>
    // [Required] rather than [Url]: the value is deliberately schemeless, so a URL check
    // would reject the very shape MinIO asks for.
    [Required]
    public string Endpoint { get; set; } = "localhost:9000";

    // The two keys are deliberately not [Required], unlike everything else here that has to
    // be present: empty is their shipped default, so requiring them would turn a build with
    // no MinIO configured — which is a normal state to be in — into one that will not start.
    // Making them required is a defensible call; it is a decision about the development
    // experience rather than a validation gap, so it is left to be made on purpose.
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool UseSsl { get; set; }

    [Required]
    public string Bucket { get; set; } = "lnshelf-covers";

    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan FetchTimeout { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>Longest edge a stored cover may have (feature-spec §4.3).</summary>
    // 1000px is about three times the largest size the UI renders, which leaves room
    // for a denser grid later without storing originals.
    [Range(1, 10000)]
    public int MaximumCoverEdge { get; set; } = 1000;

    /// <summary>
    /// The most pixels an incoming cover may decode to (width x height), checked from the
    /// header before any decoding.
    /// </summary>
    // 40 megapixels is a 5000 x 8000 scan, far past any real cover, and decodes to about
    // 160 MB of RGBA — the allocation this exists to bound. The byte limits do not: a tiny
    // compressed file can claim an enormous canvas.
    [Range(1d, 500_000_000d)]
    public long MaximumDecodedPixels { get; set; } = 40_000_000;

    /// <summary>Webp quality, 1-100.</summary>
    [Range(1, 100)]
    public int CoverQuality { get; set; } = 82;

    /// <summary>
    /// The most any cover upload request may carry: the cover endpoint's
    /// [RequestSizeLimit] and the ceiling of <see cref="MaximumUploadBytes"/>.
    /// </summary>
    // One constant for both, because they are the same limit seen from two places. The
    // option used to allow 64 MiB while the endpoint's attribute refused anything over
    // 8 MiB before the option was ever read, so configuring a higher value silently did
    // nothing. The option can still lower the limit; raising it is a code change here.
    public const long UploadCeilingBytes = 8 * 1024 * 1024;

    /// <summary>Largest upload accepted for a cover override, up to <see cref="UploadCeilingBytes"/>.</summary>
    // The double form of [Range], because a long constant can feed it (the typeof form
    // takes strings, which would have to repeat the number) and, unlike the int form, it
    // does not overflow on a configured value above int.MaxValue — that is reported as
    // out of range like any other.
    [Range(1d, UploadCeilingBytes)]
    public long MaximumUploadBytes { get; set; } = UploadCeilingBytes;
}
