using static LnShelf.Api.Tests.Features.Metadata.Services.SourceChoiceTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The count's provenance is written beside the count and nowhere else (D11).
/// </summary>
public sealed class ReleaseCountWriterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Apply_ACountWithProvenance_WritesItBesideTheCount(bool aniListChosen)
    {
        var series = CreateSeries(volumeCount: 8);
        series.VolumeCountBasis = VolumeCountBasis.AniList;

        // A choice of AniList's list stops the count acting, not being recorded: undoing the
        // choice needs it, and "How is this counted?" quotes it.
        if (aniListChosen)
        {
            series.SourceChoices.Add(Choice(SyncDiscrepancyField.VolumeCount, null, MetadataSourceName.AniList));
        }

        var info = Info(volumeCount: 9) with
        {
            CountBasis = VolumeCountBasis.EnglishPublisherNote,
            CountEvidence = "Yen On: 9 Volumes; Ongoing",
        };

        CreateWriter().Apply(series, info, NewRun(), refreshedBefore: true);

        series.ReleaseSourceVolumeCount.ShouldBe(9);
        series.VolumeCountBasis.ShouldBe(VolumeCountBasis.EnglishPublisherNote);
        series.VolumeCountEvidence.ShouldBe("Yen On: 9 Volumes; Ongoing");
    }

    // No count, nothing about the count: the provenance of the last count stands with it.
    [Fact]
    public void Apply_NoCount_LeavesTheProvenanceAlone()
    {
        var series = CreateSeries(volumeCount: 8);
        series.VolumeCountBasis = VolumeCountBasis.MangaUpdatesStatus;
        series.VolumeCountEvidence = "8 Volumes (Ongoing)";

        CreateWriter().Apply(series, Info() with { VolumeCount = null }, NewRun(), refreshedBefore: true);

        series.ReleaseSourceVolumeCount.ShouldBe(8);
        series.VolumeCountBasis.ShouldBe(VolumeCountBasis.MangaUpdatesStatus);
        series.VolumeCountEvidence.ShouldBe("8 Volumes (Ongoing)");
    }

    // Solo Leveling: IZE Press notes "13+2 Volumes", the store catalogue supplied 14 and 15,
    // and the shelf tracks fifteen. The source's own extras account for both.
    [Theory]
    [InlineData(15)]
    [InlineData(14)]
    [InlineData(13)]
    public void Apply_WholeVolumesWithinTheSourcesOwnExtras_RecordNoDiscrepancy(int tracked)
    {
        var series = CreateSeries(volumeCount: tracked);
        var run = NewRun();

        CreateWriter().Apply(series, Info(volumeCount: 13) with { ExtraVolumeCount = 2 }, run, refreshedBefore: true);

        run.Discrepancies.ShouldBeEmpty();
        series.Volumes.Count.ShouldBe(tracked);
    }

    // "13+2" grants two places, not every number past 13: a sixteenth is still a disagreement.
    [Fact]
    public void Apply_AWholeVolumePastTheSourcesExtras_IsStillRecorded()
    {
        var series = CreateSeries(volumeCount: 16);
        var run = NewRun();

        CreateWriter().Apply(series, Info(volumeCount: 13) with { ExtraVolumeCount = 2 }, run, refreshedBefore: true);

        var discrepancy = run.Discrepancies.ShouldHaveSingleItem();
        discrepancy.Field.ShouldBe(SyncDiscrepancyField.VolumeCount);
        discrepancy.Values.Select(value => (value.Source, value.Value))
            .ShouldBe([(MetadataSourceName.AniList, "14"), (MetadataSourceName.MangaUpdates, "13")]);
    }

    // Torture Princess: "9 Volumes + 1 Extra" with Vol. 7.5 tracked. The bonus volume is the
    // extra, so the old phantom Vol. 10 is a volume the source does not know of.
    [Fact]
    public void Apply_AWholeVolumeWhenABonusVolumeAlreadyFillsTheExtras_IsStillRecorded()
    {
        var series = CreateSeries(volumeCount: 10);
        series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 7.5m });
        var run = NewRun();

        CreateWriter().Apply(series, Info(volumeCount: 9) with { ExtraVolumeCount = 1 }, run, refreshedBefore: true);

        var discrepancy = run.Discrepancies.ShouldHaveSingleItem();
        discrepancy.Values.Select(value => (value.Source, value.Value))
            .ShouldBe([(MetadataSourceName.AniList, "10"), (MetadataSourceName.MangaUpdates, "9")]);
    }

    // The same series with the phantom gone: 1-9 and 7.5 agree with "9 + 1", as they always did.
    [Fact]
    public void Apply_ABonusVolumeWithinTheSourcesExtras_RecordsNoDiscrepancy()
    {
        var series = CreateSeries(volumeCount: 9);
        series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 7.5m });
        var run = NewRun();

        CreateWriter().Apply(series, Info(volumeCount: 9) with { ExtraVolumeCount = 1 }, run, refreshedBefore: true);

        run.Discrepancies.ShouldBeEmpty();
    }

    // Without stated extras nothing past the count is excused.
    [Fact]
    public void Apply_WholeVolumesPastACountWithNoExtras_AreRecorded()
    {
        var series = CreateSeries(volumeCount: 15);
        var run = NewRun();

        CreateWriter().Apply(series, Info(volumeCount: 13), run, refreshedBefore: true);

        run.Discrepancies.ShouldHaveSingleItem().Field.ShouldBe(SyncDiscrepancyField.VolumeCount);
    }

    private static ReleaseCountWriter CreateWriter()
    {
        var releaseSource = Substitute.For<IReleaseSource>();
        releaseSource.SourceName.Returns("MangaUpdates");

        return new ReleaseCountWriter(releaseSource, NullLogger<ReleaseCountWriter>.Instance);
    }

    private static SyncRun NewRun() => new() { Id = Guid.CreateVersion7(), StartedAt = DateTimeOffset.UnixEpoch };
}
