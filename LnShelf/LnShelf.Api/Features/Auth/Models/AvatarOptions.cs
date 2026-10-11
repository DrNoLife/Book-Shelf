using System.ComponentModel.DataAnnotations;

namespace LnShelf.Api.Features.Auth.Models;

/// <summary>
/// How avatars are fetched from the provider and sized, bound once through <c>IOptions&lt;T&gt;</c>.
/// Uploads share the cover upload limit (<see cref="CoverImageOptions.MaximumUploadBytes"/>).
/// </summary>
public sealed class AvatarOptions
{
    public const string SectionName = "Avatars";

    /// <summary>Longest edge a stored avatar may have.</summary>
    // Twice the largest disc the UI draws (112px on the profile header), for sharp screens.
    [Range(32, 1000)]
    public int MaximumEdge { get; set; } = 256;

    /// <summary>
    /// How long sign-in waits for the provider's picture before going on without it.
    /// </summary>
    // Short, because a person is waiting on the sign-in redirect while this runs, and the
    // picture is decoration: the next sign-in tries again.
    [Range(typeof(TimeSpan), "00:00:01", "00:00:30")]
    public TimeSpan FetchTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>The largest provider picture accepted, before normalising.</summary>
    // A provider's profile picture is a few tens of kilobytes; anything near this is not one.
    [Range(1024, 8 * 1024 * 1024)]
    public int MaximumFetchBytes { get; set; } = 2 * 1024 * 1024;
}
