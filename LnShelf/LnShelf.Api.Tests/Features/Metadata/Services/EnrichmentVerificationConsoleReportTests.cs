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

        text.ShouldContain("title ok");
        text.ShouldContain("description ok (34 chars)");
        text.ShouldContain("cover ok (600x900)");
        text.ShouldContain("titled 14/14");
        text.ShouldContain("dated 13/14");
        text.ShouldContain("largest 1400x2100");
        text.ShouldContain("VERDICT: PASS -- 1 of 1 series");
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

    private static EnrichmentVerificationReport Report(SeriesVerification series) =>
        new() { Format = Format.LightNovel, Series = [series] };

    private static SeriesVerification Passing() => new()
    {
        SearchTerm = "Saga of Tanya the Evil",
        Format = Format.LightNovel,
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
            Titled = 14,
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
