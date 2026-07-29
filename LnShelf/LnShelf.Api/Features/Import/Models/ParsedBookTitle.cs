namespace LnShelf.Api.Features.Import.Models;

/// <summary>
/// A StoryGraph book title, split into the series it belongs to and the volume
/// it is.
/// </summary>
public sealed record ParsedBookTitle
{
    /// <summary>The series as the export names it, volume part removed.</summary>
    public required string SeriesTitle { get; init; }

    /// <summary>Null when the title carried no volume number — a standalone book.</summary>
    public int? VolumeNumber { get; init; }

    /// <summary>
    /// Set when the export numbers by part ("Part 2 Volume 3"). The part is folded
    /// into <see cref="SeriesTitle"/> rather than kept separate, because that is
    /// how AniList models these: "Ascendance of a Bookworm: Part 2" is its own
    /// series there, with its own volume 1.
    /// </summary>
    public int? Part { get; init; }

    /// <summary>The volume's own subtitle, kept for display only.</summary>
    public string? Subtitle { get; init; }
}
