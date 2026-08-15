namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// What one image URL turned out to be, once it had been downloaded and decoded.
/// </summary>
// Dimensions rather than a boolean, because "the bytes arrived" is not the claim being
// made. A CDN answering 200 with an HTML error page under a .jpg URL satisfies the first
// and is exactly the failure a cover pipeline hides; only a decoder can tell them apart.
public sealed record VerifiedImage
{
    public required int ByteCount { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
}

/// <summary>
/// What the series-level seam answered about the series itself.
/// </summary>
public sealed record SeriesLevelVerification
{
    public string? Title { get; init; }
    public string? Description { get; init; }

    /// <summary>Null when the source published no cover, or the cover would not decode.</summary>
    public VerifiedImage? Cover { get; init; }

    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public bool HasCover => Cover is not null;

    public int DescriptionLength => Description?.Length ?? 0;
}

/// <summary>
/// What the enrichment pipeline managed to fill in across one series' volumes.
/// </summary>
// Counts, not shares. The thresholds live in EnrichmentVerificationService and compare
// with integer arithmetic, so nothing here has to hold a rounded percentage that a reader
// would then have to trust.
public sealed record VolumeLevelVerification
{
    public required int Total { get; init; }

    /// <summary>
    /// Volumes the catalogue named at all, whether or not the name carried a subtitle.
    /// </summary>
    // The measure of whether the pipeline reached a volume, and the one with a threshold on
    // it. Volume.Title's three states are what make it readable: a named volume with no
    // subtitle of its own stores "", which counts here and not under Titled.
    public required int Matched { get; init; }

    /// <summary>
    /// Of those, the ones whose name carried a subtitle. Informational only.
    /// </summary>
    // Deliberately not a threshold. Whole series have no volume subtitles — every volume of
    // That Time I Got Reincarnated as a Slime is titled only "…, Vol. N" — so a bar here
    // would fail a pipeline that is working perfectly, on a fact about the publisher.
    public required int Titled { get; init; }

    public required int Described { get; init; }
    public required int Covered { get; init; }
    public required int Dated { get; init; }

    /// <summary>Bytes of cover art actually pulled down and decoded during the run.</summary>
    // Reported because it is the evidence behind <see cref="Covered"/>: a count of set
    // columns proves nothing on its own, and megabytes of decoded art does.
    public required int CoverByteCount { get; init; }

    /// <summary>The largest cover the run decoded, by pixel area. Null when none decoded.</summary>
    public VerifiedImage? LargestCover { get; init; }
}

/// <summary>
/// One canary series' whole result — both levels, and how the volume count was decided.
/// </summary>
public sealed record SeriesVerification
{
    /// <summary>What was typed at the metadata source, which is what a person can re-run.</summary>
    public required string SearchTerm { get; init; }

    public required Format Format { get; init; }

    /// <summary>The metadata source's own id for the series it resolved to.</summary>
    public int? ExternalId { get; init; }

    /// <summary>Null when the search resolved to nothing, which is a failure in itself.</summary>
    public SeriesLevelVerification? SeriesLevel { get; init; }

    /// <summary>Null when no volume count could be established, so nothing was enriched.</summary>
    public VolumeLevelVerification? VolumeLevel { get; init; }

    /// <summary>Which source decided how many volumes were checked.</summary>
    public required string VolumeCountGround { get; init; }

    /// <summary>Why the release source did not answer. Non-fatal — the run falls through.</summary>
    public string? ReleaseSourceNote { get; init; }

    /// <summary>Set only when the series could not be resolved at all.</summary>
    public string? Failure { get; init; }

    public required bool Passed { get; init; }
}

/// <summary>
/// One <c>enrichment-verify</c> run: every canary, and whether the shelf's fetching
/// still works.
/// </summary>
public sealed record EnrichmentVerificationReport
{
    public required Format Format { get; init; }
    public required IReadOnlyList<SeriesVerification> Series { get; init; }

    /// <summary>Every series has to pass for the run to.</summary>
    // An empty run is deliberately not a pass: "nothing was checked" must never be
    // reported in the same word as "everything works".
    public bool Passed => Series.Count > 0 && Series.All(series => series.Passed);
}
