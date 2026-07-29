namespace LnShelf.Api.Features.Shelf.Models;

/// <summary>
/// A child of a series and the unit of user state. Volumes arrive from metadata
/// sync; <see cref="IsLocalOnly"/> marks the manual escape hatch (§4.1).
/// </summary>
public sealed class Volume
{
    public Guid Id { get; set; }
    public Guid SeriesId { get; set; }

    public int Number { get; set; }

    /// <summary>Null means announced with no date yet.</summary>
    public DateOnly? ReleaseDate { get; set; }

    public string? CoverObjectKey { get; set; }

    /// <summary>Null exactly when <see cref="IsLocalOnly"/> is true.</summary>
    public string? ExternalId { get; set; }

    public bool IsLocalOnly { get; set; }

    public VolumeUserState? UserState { get; set; }
}
