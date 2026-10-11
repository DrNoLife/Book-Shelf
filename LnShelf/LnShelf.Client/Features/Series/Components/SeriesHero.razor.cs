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

    /// <summary>
    /// The placeholder cover's "Vol N": the highest volume number tracked, 1 with none.
    /// </summary>
    // The highest number, not the tracked count: a bonus volume (7.5) makes the count run
    // one past the last volume.
    private decimal LatestVolumeNumber =>
        Series.Volumes.Count == 0 ? 1 : Series.Volumes.Max(volume => volume.Number);

    private bool IsDescriptionLong => Series.Description is { Length: > ClampThreshold };

    private bool IsDescriptionExpanded => _expandedSeriesId == Series.SeriesId;

    private void ToggleDescription() =>
        _expandedSeriesId = IsDescriptionExpanded ? null : Series.SeriesId;

    /// <summary>
    /// "Carlo Zen, illustrated by Shinobu Shinotsuki · Kadokawa", with the clauses that
    /// have no data left out rather than rendered empty. Null when none has any.
    /// </summary>
    private string? CreditsLine
    {
        get
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(Series.Author)) parts.Add(Series.Author);
            if (!string.IsNullOrWhiteSpace(Series.Illustrator))
            {
                parts.Add($"illustrated by {Series.Illustrator}");
            }

            var credits = string.Join(", ", parts);

            if (!string.IsNullOrWhiteSpace(Series.Publisher))
            {
                credits = credits.Length == 0 ? Series.Publisher : $"{credits} · {Series.Publisher}";
            }

            return credits.Length == 0 ? null : credits;
        }
    }

    /// <summary>"Since 2014" while it runs, "2014" once it has ended.</summary>
    private string? YearLine => Series.StartYear is { } startYear
        ? Series.PublicationStatus is PublicationStatus.Completed
            ? startYear.ToString(CultureInfo.InvariantCulture)
            : $"Since {startYear}"
        : null;

    /// <summary>"Yōjo Senki · 幼女戦記".</summary>
    private string SubtitleLine
    {
        get
        {
            var titles = new List<string>();
            if (!string.IsNullOrWhiteSpace(Series.TitleRomaji)) titles.Add(Series.TitleRomaji);
            if (!string.IsNullOrWhiteSpace(Series.TitleNative)) titles.Add(Series.TitleNative);

            return string.Join(" · ", titles);
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
    private string SaxoSearchUrl(decimal volumeNumber) =>
        SaxoSearch.UrlFor(Series.TitleEnglish, volumeNumber);
}
