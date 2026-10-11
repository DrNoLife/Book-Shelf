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

    private static ReleaseCountWriter CreateWriter()
    {
        var releaseSource = Substitute.For<IReleaseSource>();
        releaseSource.SourceName.Returns("MangaUpdates");

        return new ReleaseCountWriter(releaseSource, NullLogger<ReleaseCountWriter>.Instance);
    }

    private static SyncRun NewRun() => new() { Id = Guid.CreateVersion7(), StartedAt = DateTimeOffset.UnixEpoch };
}
