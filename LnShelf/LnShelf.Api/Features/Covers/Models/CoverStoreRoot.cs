namespace LnShelf.Api.Features.Covers.Models;

/// <summary>
/// The cover folder as one absolute, normalised path, worked out once at startup.
/// </summary>
// Its own type so the store and the startup probe agree on the folder without each resolving
// the option again, and so the one normalisation rule (absolute, no trailing separator) lives
// in one place.
public sealed class CoverStoreRoot
{
    public CoverStoreRoot(IOptions<FileSystemCoverStoreOptions> options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        var configured = options.Value.RootPath;
        var absolute = System.IO.Path.IsPathFullyQualified(configured)
            ? System.IO.Path.GetFullPath(configured)
            : System.IO.Path.GetFullPath(configured, environment.ContentRootPath);

        Path = System.IO.Path.TrimEndingDirectorySeparator(absolute);
    }

    public string Path { get; }

    /// <summary>Where writes are staged before their atomic rename into place.</summary>
    // Inside the root so the rename stays on one filesystem (which is what makes it atomic),
    // and a dot folder, which no key can name.
    public string StagingPath => System.IO.Path.Combine(Path, ".tmp");
}
