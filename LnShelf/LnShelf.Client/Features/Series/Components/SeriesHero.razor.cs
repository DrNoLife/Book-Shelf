namespace LnShelf.Client.Features.Series.Components;

public partial class SeriesHero
{
    /// <summary>
    /// Longer than this and the synopsis is clamped behind a "More" toggle.
    /// </summary>
    // A character count rather than a line count, because the component cannot measure
    // lines: the clamp itself is CSS, and this only decides whether the toggle is offered.
    // ~500 characters is roughly the four clamped lines at the hero's measure, so a synopsis
    // that would not have been cut does not get a button that does nothing.
    private const int ClampThreshold = 500;

    /// <summary>
    /// The series whose synopsis the reader has opened, or null for none.
    /// </summary>
    // Keyed on the series rather than held as a bool: the same component instance is reused
    // when navigating from one series to the next, and a bare flag would open the new
    // series' synopsis because the reader expanded the previous one's.
    private Guid? _expandedSeriesId;

    [Parameter]
    public required SeriesDetailResponse Series { get; set; }

    /// <summary>
    /// The synopsis split into paragraphs. Empty when there is nothing to say, which is
    /// what makes the whole block disappear rather than render an empty panel.
    /// </summary>
    // The API sends plain text with a blank line between paragraphs (the metadata slice's
    // sanitizer guarantees the shape), so splitting is all the client has to do.
    private IReadOnlyList<string> DescriptionParagraphs =>
        Series.Description is { } description
            ? description.Split(
                "\n\n",
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

    private bool IsDescriptionLong => Series.Description is { Length: > ClampThreshold };

    private bool IsDescriptionExpanded => _expandedSeriesId == Series.SeriesId;

    private void ToggleDescription() =>
        _expandedSeriesId = IsDescriptionExpanded ? null : Series.SeriesId;

    /// <summary>
    /// "Kadokawa · 2013 – · 14 volumes out", with the clauses that have no data left
    /// out rather than rendered empty. AniList supplies no publisher, so a real series
    /// usually starts at the year.
    /// </summary>
    private string FactsLine
    {
        get
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(Series.Publisher))
            {
                parts.Add(Series.Publisher);
            }

            if (Series.StartYear is { } startYear)
            {
                parts.Add(Series.PublicationStatus is PublicationStatus.Completed
                    ? startYear.ToString(CultureInfo.InvariantCulture)
                    : $"{startYear} –");
            }

            parts.Add($"{Series.Counts.Published} volumes out");
            return string.Join(" · ", parts);
        }
    }

    /// <summary>
    /// "Yōjo Senki · 幼女戦記 — Carlo Zen, illustrated by Shinobu Shinotsuki".
    /// </summary>
    private string SubtitleLine
    {
        get
        {
            var titles = new List<string>();
            if (!string.IsNullOrWhiteSpace(Series.TitleRomaji)) titles.Add(Series.TitleRomaji);
            if (!string.IsNullOrWhiteSpace(Series.TitleNative)) titles.Add(Series.TitleNative);

            var credits = new List<string>();
            if (!string.IsNullOrWhiteSpace(Series.Author)) credits.Add(Series.Author);
            if (!string.IsNullOrWhiteSpace(Series.Illustrator))
            {
                credits.Add($"illustrated by {Series.Illustrator}");
            }

            var left = string.Join(" · ", titles);
            var right = string.Join(", ", credits);

            return (left.Length, right.Length) switch
            {
                (0, 0) => string.Empty,
                (0, _) => right,
                (_, 0) => left,
                _ => $"{left} — {right}",
            };
        }
    }

    /// <summary>
    /// The mockup's "3 sitting unread. 3 to buy." — in the fixed terminology, and
    /// honest when both numbers are zero.
    /// </summary>
    private string ProseLine
    {
        get
        {
            var counts = Series.Counts;

            if (counts.Backlog == 0 && counts.ToBuy == 0)
            {
                return counts.NotOut > 0
                    ? $"Caught up. {counts.NotOut} more to come."
                    : "Caught up, and that is the whole series.";
            }

            var parts = new List<string>();
            if (counts.Backlog > 0) parts.Add($"{counts.Backlog} sitting unread.");
            if (counts.ToBuy > 0) parts.Add($"{counts.ToBuy} to buy.");

            return string.Join(" ", parts);
        }
    }

    /// <summary>"Started 12 Mar 2023 · last read 6 Jul 2026".</summary>
    private string? ReadingLine
    {
        get
        {
            var parts = new List<string>();
            if (Series.StartedAt is { } started) parts.Add($"Started {started.Full}");
            if (Series.LastReadAt is { } lastRead) parts.Add($"last read {lastRead.Full}");

            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }
    }

    private static string RelationLabel(SeriesRelationResponse relation) => relation.Format switch
    {
        Format.Manga => "The manga adaptation",
        Format.LightNovel => "The light novel",
        _ => relation.TitleEnglish,
    };

    /// <summary>
    /// The CTA design.md specifies. The URL itself is built in one shared place, since
    /// the library's rows view now prints the same link.
    /// </summary>
    private string SaxoSearchUrl(int volumeNumber) =>
        SaxoSearch.UrlFor(Series.TitleEnglish, volumeNumber);
}
