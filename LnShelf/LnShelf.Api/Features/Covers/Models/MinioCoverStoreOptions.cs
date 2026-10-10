using System.ComponentModel.DataAnnotations;

namespace LnShelf.Api.Features.Covers.Models;

/// <summary>
/// Where the retired MinIO cover store is, for <c>cover-migrate</c> to copy out of.
/// </summary>
// Only bound when Covers:Minio:Endpoint is set (CoversFeatureExtensions), so a machine with no
// MinIO, which is every machine once the migration has run, starts without it. The keys are
// credentials and arrive as environment variables.
public sealed class MinioCoverStoreOptions
{
    public const string SectionName = "Covers:Minio";

    /// <summary>Host and port, S3 style, no scheme.</summary>
    // [Required] rather than [Url]: the value is deliberately schemeless, so a URL check
    // would reject the very shape MinIO asks for.
    [Required]
    public string Endpoint { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public bool UseSsl { get; set; }

    [Required]
    public string Bucket { get; set; } = "lnshelf-covers";
}
