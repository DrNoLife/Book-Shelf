using System.ComponentModel.DataAnnotations;

namespace LnShelf.Api.Features.Covers.Models;

/// <summary>
/// Where <see cref="Services.FileSystemCoverStore"/> keeps the covers.
/// </summary>
public sealed class FileSystemCoverStoreOptions
{
    public const string SectionName = "Covers:FileSystem";

    /// <summary>
    /// The folder covers live under. A relative path is taken from the API's content root,
    /// so development can point at the repository's git-ignored <c>.data/covers</c>.
    /// </summary>
    // The default is where the API image mounts its covers volume (Dockerfile.api), so
    // production needs no setting. Required, so configuring it empty is refused at startup
    // rather than quietly meaning the content root.
    [Required]
    public string RootPath { get; set; } = "/data/covers";
}
