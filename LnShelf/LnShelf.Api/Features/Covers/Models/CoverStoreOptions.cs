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
}
