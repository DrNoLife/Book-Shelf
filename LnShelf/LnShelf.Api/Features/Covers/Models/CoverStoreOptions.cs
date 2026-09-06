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

    /// <summary>Webp quality, 1-100.</summary>
    [Range(1, 100)]
    public int CoverQuality { get; set; } = 82;

    /// <summary>Largest upload accepted for a cover override. 64 MiB ceiling.</summary>
    // The typeof(long) form of [Range], because the int overload converts the value
    // through Convert.ToInt32 and a configured value above int.MaxValue would surface as
    // an OverflowException from inside validation instead of a named field.
    [Range(typeof(long), "1", "67108864")]
    public long MaximumUploadBytes { get; set; } = 8 * 1024 * 1024;
}
