using System.Globalization;
using LnShelf.Shared.Shelf;
using Microsoft.AspNetCore.Components;

namespace LnShelf.Client.Features.Series.Components;

public partial class SeriesHero
{
    [Parameter]
    public required SeriesDetailResponse Series { get; set; }

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
            if (Series.StartedAt is { } started) parts.Add($"Started {FormatDate(started)}");
            if (Series.LastReadAt is { } lastRead) parts.Add($"last read {FormatDate(lastRead)}");

            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }
    }

    private static string FormatDate(DateOnly date) =>
        date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static string RelationLabel(SeriesRelationResponse relation) => relation.Format switch
    {
        Format.Manga => "The manga adaptation",
        Format.LightNovel => "The light novel",
        _ => relation.TitleEnglish,
    };

    /// <summary>
    /// A Saxo search built from title plus volume, client-side — the CTA design.md
    /// specifies, with no affiliate plumbing behind it.
    /// </summary>
    private string SaxoSearchUrl(int volumeNumber) =>
        "https://www.saxo.com/dk/products/search?query="
        + Uri.EscapeDataString($"{Series.TitleEnglish} {volumeNumber}");
}
