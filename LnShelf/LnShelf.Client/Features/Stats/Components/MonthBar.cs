namespace LnShelf.Client.Features.Stats.Components;

/// <summary>One month's column in a <see cref="MonthBarChart"/>.</summary>
/// <param name="Month">The first day of the month.</param>
/// <param name="Segments">Bottom to top. Zero-valued segments are skipped when drawn.</param>
/// <param name="ValueLabel">Printed above the column, so the figure is never hover-only.</param>
public sealed record MonthBar(DateOnly Month, IReadOnlyList<BarSegment> Segments, string ValueLabel)
{
    public decimal Total => Segments.Sum(segment => segment.Value);
}

/// <param name="Kind">Picks the fill — one of the three format marks.</param>
/// <param name="Words">What the segment counts, for the column's accessible name.</param>
public sealed record BarSegment(decimal Value, BarSegmentKind Kind, string Words);

/// <summary>
/// The segment fills. They are the format badges' own mapping, so a chart split by format
/// speaks the palette the rest of the app already taught: LN persimmon, manga ink, WN
/// outlined — which also means WN survives grayscale as a shape, not a shade.
/// </summary>
public enum BarSegmentKind
{
    LightNovel,
    Manga,
    WebNovel,

    /// <summary>A chart with one series (spend) — neutral ink, because it names no format.</summary>
    Plain,
}
