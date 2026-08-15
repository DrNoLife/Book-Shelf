namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The table <c>enrichment-verify</c> prints. Its whole output is what a person reads, so
/// the numbers reaching the page are worth asserting.
/// </summary>
public sealed class EnrichmentVerificationConsoleReportTests
{
    [Fact]
    public void Render_APassingSeries_PrintsBothRowsAndTheVerdict()
    {
        var lines = EnrichmentVerificationConsoleReport.Render(Report(Passing()));
        var text = string.Join(Environment.NewLine, lines);

        // Which catalogue resolved the series, beside the id it resolved to.
        text.ShouldContain("[AniList 94846]");

        text.ShouldContain("title ok");
        text.ShouldContain("description ok (34 chars)");
        text.ShouldContain("cover ok (600x900)");
        text.ShouldContain("matched 14/14");
        text.ShouldContain("dated 13/14");
        text.ShouldContain("largest 1400x2100");
        text.ShouldContain("VERDICT: PASS -- 1 of 1 series");
    }

    [Fact]
    public void Render_TheSubtitleCount_IsPrintedApartAndSaidToBeInformational()
    {
        var noSubtitles = Passing() with
        {
            VolumeLevel = Passing().VolumeLevel! with { Titled = 0 },
        };

        var text = string.Join(Environment.NewLine, EnrichmentVerificationConsoleReport.Render(Report(noSubtitles)));

        // A series whose publisher gives its volumes no subtitles reads 0 of 14 and is
        // working perfectly, so the line has to say that nothing is judged against it.
        text.ShouldContain("0/14 carry a subtitle -- informational, no threshold");
        text.ShouldNotContain("titled >=");
        text.ShouldContain("VERDICT: PASS");
    }

    [Fact]
    public void Render_ASeriesThatWasNeverResolved_SaysWhyAndFails()
    {
        var unresolved = new SeriesVerification
        {
            SearchTerm = "a series nobody has heard of",
            Format = Format.LightNovel,
            VolumeCountGround = "none (the series was never resolved)",
            Failure = "the search returned no LightNovel.",
            Passed = false,
        };

        var text = string.Join(Environment.NewLine, EnrichmentVerificationConsoleReport.Render(Report(unresolved)));

        // The search phrase stands in for a title there is none of, so the line still says
        // which of the checks failed.
        text.ShouldContain("a series nobody has heard of");
        text.ShouldContain("the search returned no LightNovel.");
        text.ShouldContain("VERDICT: FAIL -- 0 of 1 series");
    }

    [Fact]
    public void Render_AnUnlinkedSeries_StillPrintsItsCountsAndSaysTheReleaseSourceMissed()
    {
        var unlinked = Passing() with
        {
            VolumeCountGround = "14 from the store catalogue's highest confirmed volume",
            ReleaseSourceNote = "unlinked (NoCandidate: MangaUpdates returned nothing)",
        };

        var text = string.Join(Environment.NewLine, EnrichmentVerificationConsoleReport.Render(Report(unlinked)));

        // Non-fatal by design: an unlinked series still has volumes worth verifying.
        text.ShouldContain("unlinked (NoCandidate");
        text.ShouldContain("VERDICT: PASS");
    }

    [Fact]
    public void Render_ASeriesResolvedThroughTheFallback_NamesMangaUpdatesInItsIdentity()
    {
        // A run through the fallback can pass every threshold — the per-volume pipeline does
        // not care where the title came from — so the source is what stops a degraded PASS
        // from reading exactly like a healthy one.
        var throughFallback = Passing() with
        {
            Source = MetadataSourceName.MangaUpdates,
            ExternalId = 17992364412,
            SeriesLevel = Passing().SeriesLevel! with { Title = "Fremd Torturchen" },
        };

        var text = string.Join(
            Environment.NewLine, EnrichmentVerificationConsoleReport.Render(Report(throughFallback)));

        text.ShouldContain("Fremd Torturchen [MangaUpdates 17992364412]");
        text.ShouldContain("VERDICT: PASS");
    }

    [Fact]
    public void Render_ASeriesThatResolvedToNothing_StillNamesTheSourceThatWasAsked()
    {
        // "MangaUpdates has never heard of it" and "AniList has never heard of it" are
        // different facts, and during an AniList outage only one of them is possible.
        var unresolved = new SeriesVerification
        {
            SearchTerm = "86 eighty six",
            Format = Format.LightNovel,
            Source = MetadataSourceName.MangaUpdates,
            VolumeCountGround = "none (the series was never resolved)",
            Failure = "the search returned no LightNovel.",
            Passed = false,
        };

        var text = string.Join(
            Environment.NewLine, EnrichmentVerificationConsoleReport.Render(Report(unresolved)));

        text.ShouldContain("86 eighty six [MangaUpdates]");
    }

    private static EnrichmentVerificationReport Report(SeriesVerification series) =>
        new() { Format = Format.LightNovel, Series = [series] };

    private static SeriesVerification Passing() => new()
    {
        SearchTerm = "Saga of Tanya the Evil",
        Format = Format.LightNovel,
        Source = MetadataSourceName.AniList,
        ExternalId = 94846,
        SeriesLevel = new SeriesLevelVerification
        {
            Title = "The Saga of Tanya the Evil",
            Description = "A blurb long enough to be a blurb.",
            Cover = new VerifiedImage { ByteCount = 1024, Width = 600, Height = 900 },
        },
        VolumeLevel = new VolumeLevelVerification
        {
            Total = 14,
            Matched = 14,
            Titled = 9,
            Described = 14,
            Covered = 14,
            Dated = 13,
            CoverByteCount = 3_500_000,
            LargestCover = new VerifiedImage { ByteCount = 400_000, Width = 1400, Height = 2100 },
        },
        VolumeCountGround = "14 from the release source",
        Passed = true,
    };
}
