namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// What the backfill managed for one series' volumes.
/// </summary>
public sealed record SubtitleBackfillSeriesResult
{
    public required string SeriesTitle { get; init; }

    /// <summary>Volumes a lookup was actually spent on — one each.</summary>
    public required int Asked { get; init; }

    /// <summary>Volumes whose empty title became a real subtitle.</summary>
    public required int Subtitled { get; init; }

    /// <summary>
    /// Records that named the volume and gave it no subtitle of its own. Left as
    /// <c>""</c>, which is what it already said.
    /// </summary>
    public required int WithoutSubtitle { get; init; }

    /// <summary>
    /// Volumes the by-ISBN source had no usable record for: no record at all, or one
    /// naming a language other than English.
    /// </summary>
    // The two are folded together on purpose, and the nightly pass does the same thing
    // with them (both are one EnrichmentGaps). Neither is an error and neither settles
    // anything about the subtitle; the difference between them is in the log line, which
    // says which of the two it was.
    public required int NotIndexed { get; init; }

    /// <summary>Lookups that threw. One failure never stops the run.</summary>
    public required int Failed { get; init; }

    public string Summary =>
        $"{SeriesTitle}: asked {Asked} · subtitled {Subtitled} · no subtitle {WithoutSubtitle} · "
        + $"not indexed {NotIndexed} · failed {Failed}";
}

/// <summary>
/// One <c>subtitle-backfill</c> run. Written so a second run can be seen to have
/// found nothing new.
/// </summary>
public sealed record SubtitleBackfillReport
{
    public required IReadOnlyList<SubtitleBackfillSeriesResult> Series { get; init; }

    public int Asked => Series.Sum(result => result.Asked);
    public int Subtitled => Series.Sum(result => result.Subtitled);
    public int WithoutSubtitle => Series.Sum(result => result.WithoutSubtitle);
    public int NotIndexed => Series.Sum(result => result.NotIndexed);
    public int Failed => Series.Sum(result => result.Failed);

    public string Summary =>
        $"{Series.Count} series · asked {Asked} · subtitled {Subtitled} · no subtitle {WithoutSubtitle} · "
        + $"not indexed {NotIndexed} · failed {Failed}";
}
