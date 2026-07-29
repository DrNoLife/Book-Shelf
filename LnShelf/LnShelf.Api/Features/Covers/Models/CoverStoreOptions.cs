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
    public string Endpoint { get; set; } = "localhost:9000";

    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool UseSsl { get; set; }

    public string Bucket { get; set; } = "lnshelf-covers";

    public TimeSpan FetchTimeout { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>Longest edge a stored cover may have (feature-spec §4.3).</summary>
    // 1000px is about three times the largest size the UI renders, which leaves room
    // for a denser grid later without storing originals.
    public int MaximumCoverEdge { get; set; } = 1000;

    /// <summary>Webp quality, 1-100.</summary>
    public int CoverQuality { get; set; } = 82;

    /// <summary>Largest upload accepted for a cover override.</summary>
    public long MaximumUploadBytes { get; set; } = 8 * 1024 * 1024;
}
