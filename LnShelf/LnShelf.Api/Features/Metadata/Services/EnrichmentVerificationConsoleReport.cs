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
            $"Thresholds: the series needs all three; volumes need titled >={EnrichmentVerificationService.MinimumTitledPercent}%, "
            + $"described >={EnrichmentVerificationService.MinimumDescribedPercent}%, "
            + $"covered >={EnrichmentVerificationService.MinimumCoveredPercent}%, "
            + $"dated >={EnrichmentVerificationService.MinimumDatedPercent}%.");

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

    private static string Heading(SeriesVerification series)
    {
        var title = series.SeriesLevel?.Title ?? series.SearchTerm;
        var identity = series.ExternalId is { } externalId ? $" [{externalId}]" : string.Empty;

        return $"{title}{identity} -- volumes: {series.VolumeCountGround}";
    }

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
            $"titled {Fraction(level?.Titled, level?.Total)}",
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
