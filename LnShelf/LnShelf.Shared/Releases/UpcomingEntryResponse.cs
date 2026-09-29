using LnShelf.Shared.Shelf;

namespace LnShelf.Shared.Releases;

/// <summary>
/// One line of the Upcoming view: a series and the volumes of it the line is about.
/// </summary>
// One shape for every group. The "Out now" and "Announced" groups hold one entry per
// series with every relevant volume number on it — a series eight volumes behind is one
// line saying so, not eight lines — while the dated groups hold one entry per volume,
// because two volumes of one series on two different dates are two different facts.
public sealed record UpcomingEntryResponse
{
    public required Guid SeriesId { get; init; }
    public required string SeriesTitle { get; init; }
    public required Format Format { get; init; }

    /// <summary>Key for <c>/api/covers/{objectKey}</c>. Null renders the placeholder.</summary>
    public string? CoverObjectKey { get; init; }

    /// <summary>The volumes this line is about, ascending. Never empty.</summary>
    public required IReadOnlyList<int> VolumeNumbers { get; init; }

    /// <summary>
    /// The volume's own subtitle, on a dated single-volume line when a source named it.
    /// </summary>
    public string? VolumeTitle { get; init; }

    /// <summary>Set on the dated groups; null on "Out now" and "Announced".</summary>
    public DateOnly? ReleaseDate { get; init; }
}
