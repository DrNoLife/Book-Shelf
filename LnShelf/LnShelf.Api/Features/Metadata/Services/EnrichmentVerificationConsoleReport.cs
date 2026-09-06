namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Renders an <see cref="EnrichmentVerificationReport"/> as the lines the command prints.
/// </summary>
// Separate from the command so the table can be asserted without running anything, and
// separate from the service because judging a run and describing one are different jobs.
//
// ASCII only, deliberately. A tick would read better than the word "ok", and on Windows the
// console's code page turns U+2713 into a question mark — a verification report that prints
// "?" where it means "yes" is worse than a plain one.
public static class EnrichmentVerificationConsoleReport
{
    private const string Present = "ok";
    private const string Absent = "MISSING";

    public static IReadOnlyList<string> Render(EnrichmentVerificationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var seriesRows = report.Series.Select(SeriesRow).ToList();
        var volumeRows = report.Series.Select(VolumeRow).ToList();

        var seriesWidths = ColumnWidths(seriesRows);
        var volumeWidths = ColumnWidths(volumeRows);

        // The two row kinds have different columns and so are measured separately — except
        // the label, which is the one column they share and the one a reader's eye follows
        // down the block.
        AlignLabels(seriesWidths, volumeWidths);

        var lines = new List<string>
        {
            $"enrichment-verify -- {report.Format}, {report.Series.Count} series",
            string.Empty,
        };

        for (var index = 0; index < report.Series.Count; index++)
        {
            lines.AddRange(RenderSeries(
                report.Series[index], seriesRows[index], seriesWidths, volumeRows[index], volumeWidths));

            lines.Add(string.Empty);
        }

        lines.Add(
            $"Thresholds: the series needs all three; volumes need matched >={VerificationVerdict.MinimumMatchedPercent}%, "
            + $"described >={VerificationVerdict.MinimumDescribedPercent}%, "
            + $"covered >={VerificationVerdict.MinimumCoveredPercent}%, "
            + $"dated >={VerificationVerdict.MinimumDatedPercent}%.");

        var passed = report.Series.Count(series => series.Passed);
        lines.Add($"VERDICT: {Verdict(report.Passed)} -- {passed} of {report.Series.Count} series");

        return lines;
    }

    private static IEnumerable<string> RenderSeries(
        SeriesVerification series,
        string[] seriesRow,
        int[] seriesWidths,
        string[] volumeRow,
        int[] volumeWidths)
    {
        yield return Heading(series);

        if (series.Failure is { } failure)
        {
            yield return $"  {failure}";
            yield return $"  {Verdict(series.Passed)}";
            yield break;
        }

        yield return Pad(seriesRow, seriesWidths);
        yield return Pad(volumeRow, volumeWidths);

        // Its own line rather than a fifth column, and labelled, because it is the one
        // number here that nothing is judged against: a series whose publisher gives its
        // volumes no subtitles reads 0 of N and is working perfectly.
        if (series.VolumeLevel is { } counted)
        {
            yield return Pad(
                [
                    "titled",
                    $"{Fraction(counted.Titled, counted.Total)} carry a subtitle "
                    + "-- informational, no threshold",
                ],
                seriesWidths);
        }

        // Its own line, and printed whether or not a title won: which name the store answered
        // to is the difference between a shelf that works and one that has been asking under
        // a romaji title all along, and neither reads off the volume counts above.
        if (series.VolumeLevel is not null)
        {
            // Labelled "store" rather than "catalogue" so it fits the label column the two
            // measured rows set, which is the column a reader's eye follows down the block. It
            // also pairs with "release" below: one line per source that had to be asked.
            yield return Pad(["store", CatalogueOutcome(series)], seriesWidths);
        }

        if (series.VolumeLevel is { LargestCover: { } largest } volumeLevel)
        {
            yield return Pad(
            [
                "art",
                $"{Megabytes(volumeLevel.CoverByteCount)} downloaded and decoded, "
                + $"largest {largest.Width}x{largest.Height}",
            ],
                seriesWidths);
        }

        if (series.ReleaseSourceNote is { } note)
        {
            yield return Pad(["release", note], seriesWidths);
        }

        yield return $"  {Verdict(series.Passed)}";
    }

    /// <summary>
    /// The series' line: what it resolved to, on whose authority, and how many volumes.
    /// </summary>
    // The source is printed here rather than as a fifth cell in the series row, because it
    // belongs with the identity it qualifies: "[MangaUpdates 17992364412]" is one fact, and
    // splitting the catalogue away from the number in it would read as two.
    //
    // It earns its place because the source is no longer fixed. A run whose series were
    // resolved through the MangaUpdates fallback can pass every threshold — the per-volume
    // pipeline does not care where the title came from — and without this the reader would
    // have no way to tell that AniList was down while it did.
    private static string Heading(SeriesVerification series)
    {
        var title = series.SeriesLevel?.Title ?? series.SearchTerm;

        var identity = (series.Source, series.ExternalId) switch
        {
            ({ } source, { } externalId) => $" [{source} {externalId}]",
            ({ } source, null) => $" [{source}]",
            _ => string.Empty,
        };

        return $"{title}{identity} -- volumes: {series.VolumeCountGround}";
    }

    /// <summary>Which of the series' known titles the store answered to, if any did.</summary>
    private static string CatalogueOutcome(SeriesVerification series) =>
        series.CatalogueTitle is { } accepted
            ? $"answered under \"{accepted}\" -- of {series.KnownTitleCount} title(s) known"
            : $"{Absent} -- none of the {series.KnownTitleCount} title(s) known was recognised";

    private static string[] SeriesRow(SeriesVerification series)
    {
        var level = series.SeriesLevel;

        return
        [
            "series",
            $"title {Mark(level?.HasTitle is true)}",
            level?.HasDescription is true
                ? $"description {Present} ({level.DescriptionLength} chars)"
                : $"description {Absent}",
            level?.Cover is { } cover ? $"cover {Present} ({cover.Width}x{cover.Height})" : $"cover {Absent}",
        ];
    }

    private static string[] VolumeRow(SeriesVerification series)
    {
        var level = series.VolumeLevel;

        return
        [
            "volumes",
            $"matched {Fraction(level?.Matched, level?.Total)}",
            $"described {Fraction(level?.Described, level?.Total)}",
            $"covered {Fraction(level?.Covered, level?.Total)}",
            $"dated {Fraction(level?.Dated, level?.Total)}",
        ];
    }

    private static string Fraction(int? count, int? total) =>
        $"{count ?? 0}/{total ?? 0}";

    private static string Mark(bool present) => present ? Present : Absent;

    private static string Verdict(bool passed) => passed ? "PASS" : "FAIL";

    private static string Megabytes(int byteCount) =>
        $"{(byteCount / 1024d / 1024d).ToString("0.0", CultureInfo.InvariantCulture)} MB";

    private static void AlignLabels(int[] seriesWidths, int[] volumeWidths)
    {
        if (seriesWidths.Length is 0 || volumeWidths.Length is 0)
        {
            return;
        }

        var widest = Math.Max(seriesWidths[0], volumeWidths[0]);
        seriesWidths[0] = widest;
        volumeWidths[0] = widest;
    }

    /// <summary>The widest cell in each column, so the same column lines up across series.</summary>
    private static int[] ColumnWidths(IReadOnlyList<string[]> rows)
    {
        if (rows.Count is 0)
        {
            return [];
        }

        var widths = new int[rows[0].Length];

        foreach (var row in rows)
        {
            for (var column = 0; column < widths.Length; column++)
            {
                widths[column] = Math.Max(widths[column], row[column].Length);
            }
        }

        return widths;
    }

    private static string Pad(string[] row, int[] widths)
    {
        var builder = new StringBuilder("  ");

        for (var column = 0; column < row.Length; column++)
        {
            builder.Append(row[column].PadRight(widths[column]));

            if (column < row.Length - 1)
            {
                builder.Append("   ");
            }
        }

        return builder.ToString().TrimEnd();
    }
}
