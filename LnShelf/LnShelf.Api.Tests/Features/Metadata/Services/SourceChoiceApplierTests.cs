using static LnShelf.Api.Tests.Features.Metadata.Services.SourceChoiceTestContext;
using static LnShelf.Api.Tests.Features.Metadata.Services.VolumeEnrichmentTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The first application of a choice, and taking it back: what lands in the synced columns,
/// and that a reset puts them exactly where automatic sync left them.
/// </summary>
public sealed class SourceChoiceApplierTests
{
    private readonly SourceChoiceApplier _applier = new();

    [Theory]
    [InlineData(MetadataSourceName.AppleBooks)]
    [InlineData(MetadataSourceName.OpenLibrary)]
    public void Apply_ReleaseDateChoiceOfADateSource_WritesTheRecordedDateAndClearsIsReleased(
        MetadataSourceName source)
    {
        var series = CreateSeries();
        var choice = Choice(SyncDiscrepancyField.ReleaseDate, 8, source);

        _applier.Apply(series, choice, DateRow(source, "2099-03-09"));

        var volume = VolumeEight(series);
        volume.ReleaseDate.ShouldBe(FirstDate);
        volume.ReleaseDateSource.ShouldBe(source);

        // The date decides whether the volume is out, not the count.
        volume.IsReleased.ShouldBeFalse();
    }

    [Fact]
    public void Apply_ReleaseDateChoiceOfMangaUpdates_ChangesNothing()
    {
        var series = CreateSeries();

        _applier.Apply(
            series,
            Choice(SyncDiscrepancyField.ReleaseDate, 8, MetadataSourceName.MangaUpdates),
            DateRow(MetadataSourceName.AppleBooks, "2099-03-09"));

        VolumeEight(series).ReleaseDate.ShouldBeNull();
        VolumeEight(series).IsReleased.ShouldBeTrue();
    }

    [Fact]
    public void Apply_VolumeCountChoiceOfAniList_MarksTheSyncedVolumesOutAndNotTheLocalOnes()
    {
        var series = CreateSeries(volumeCount: 4, released: false);
        series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 5, IsLocalOnly = true });

        _applier.Apply(series, Choice(SyncDiscrepancyField.VolumeCount, null, MetadataSourceName.AniList), recorded: null);

        series.Volumes.Where(volume => !volume.IsLocalOnly).ShouldAllBe(volume => volume.IsReleased);
        series.Volumes.Single(volume => volume.IsLocalOnly).IsReleased.ShouldBeFalse();
    }

    [Fact]
    public void Apply_VolumeCountChoiceOfMangaUpdates_ChangesNothing()
    {
        var series = CreateSeries(volumeCount: 4, released: false);

        _applier.Apply(series, Choice(SyncDiscrepancyField.VolumeCount, null, MetadataSourceName.MangaUpdates), recorded: null);

        series.Volumes.ShouldAllBe(volume => !volume.IsReleased);
    }

    [Fact]
    public void Apply_StatusChoiceOfMangaUpdates_WritesTheStatusAndRemembersTheDisplacedOne()
    {
        var series = CreateSeries(status: PublicationStatus.Completed);
        var choice = Choice(SyncDiscrepancyField.Status, null, MetadataSourceName.MangaUpdates);

        _applier.Apply(series, choice, StatusRow("Completed", "Hiatus"));

        series.PublicationStatus.ShouldBe(PublicationStatus.Hiatus);

        // The nightly sweep never re-reads AniList, so this is the only place Completed lives.
        choice.ReplacedValue.ShouldBe("Completed");
    }

    [Fact]
    public void Apply_StatusChoiceOfAniList_ChangesNothing()
    {
        var series = CreateSeries(status: PublicationStatus.Completed);
        var choice = Choice(SyncDiscrepancyField.Status, null, MetadataSourceName.AniList);

        _applier.Apply(series, choice, StatusRow("Completed", "Hiatus"));

        series.PublicationStatus.ShouldBe(PublicationStatus.Completed);
        choice.ReplacedValue.ShouldBeNull();
    }

    // Rows recorded before statuses were written as words say "not completed", which names no
    // status to write. The choice is kept, and the next refresh applies it from the source's
    // fresh answer.
    [Fact]
    public void Apply_LegacyNotCompletedValue_SavesTheChoiceAndWritesNothing()
    {
        var series = CreateSeries(status: PublicationStatus.Completed);
        var choice = Choice(SyncDiscrepancyField.Status, null, MetadataSourceName.MangaUpdates);

        _applier.Apply(series, choice, StatusRow("Completed", "not completed"));

        series.PublicationStatus.ShouldBe(PublicationStatus.Completed);
        choice.ReplacedValue.ShouldBe("Completed");
    }

    [Theory]
    [InlineData(8, true)]
    [InlineData(6, false)]
    [InlineData(null, true)]
    public void Revert_ReleaseDateChoice_ClearsTheChosenDateAndRestoresIsReleasedFromTheCount(
        int? releaseSourceVolumeCount, bool expectedIsReleased)
    {
        var series = CreateSeries();
        series.ReleaseSourceVolumeCount = releaseSourceVolumeCount;
        var choice = Choice(SyncDiscrepancyField.ReleaseDate, 8, MetadataSourceName.AppleBooks);
        _applier.Apply(series, choice, DateRow(MetadataSourceName.AppleBooks, "2099-03-09"));

        _applier.Revert(series, choice, recorded: null);

        var volume = VolumeEight(series);
        volume.ReleaseDate.ShouldBeNull();
        volume.ReleaseDateSource.ShouldBeNull();
        volume.IsReleased.ShouldBe(expectedIsReleased);
    }

    [Fact]
    public void Revert_ReleaseDateChoice_WithAStandingAniListCountChoice_KeepsTheVolumeOut()
    {
        var series = CreateSeries();
        series.ReleaseSourceVolumeCount = 6;
        var countChoice = Choice(SyncDiscrepancyField.VolumeCount, null, MetadataSourceName.AniList);
        series.SourceChoices.Add(countChoice);
        var dateChoice = Choice(SyncDiscrepancyField.ReleaseDate, 8, MetadataSourceName.AppleBooks);
        _applier.Apply(series, dateChoice, DateRow(MetadataSourceName.AppleBooks, "2099-03-09"));

        _applier.Revert(series, dateChoice, recorded: null);

        // Volume 8 is above MangaUpdates' count of 6, but the AniList choice calls it out.
        VolumeEight(series).IsReleased.ShouldBeTrue();
    }

    [Fact]
    public void Revert_ReleaseDateChoice_KeepsADateTheChosenSourceDidNotWrite()
    {
        var series = CreateSeries();
        var volume = VolumeEight(series);
        volume.ReleaseDate = new DateOnly(2021, 8, 24);
        volume.ReleaseDateSource = MetadataSourceName.OpenLibrary;

        _applier.Revert(series, Choice(SyncDiscrepancyField.ReleaseDate, 8, MetadataSourceName.AppleBooks), recorded: null);

        volume.ReleaseDate.ShouldBe(new DateOnly(2021, 8, 24));
        volume.ReleaseDateSource.ShouldBe(MetadataSourceName.OpenLibrary);
    }

    [Fact]
    public void Revert_VolumeCountAniList_UnmarksOnlyVolumesAboveTheReleaseSourceCount()
    {
        var series = CreateSeries(volumeCount: 18, released: false);
        series.ReleaseSourceVolumeCount = 16;
        var choice = Choice(SyncDiscrepancyField.VolumeCount, null, MetadataSourceName.AniList);
        _applier.Apply(series, choice, recorded: null);

        _applier.Revert(series, choice, recorded: null);

        // Overlord: MangaUpdates counts 16, so 17 and 18 are not out again, and 1 to 16 stay
        // out because the count itself said so.
        series.Volumes.Where(volume => volume.Number <= 16).ShouldAllBe(volume => volume.IsReleased);
        series.Volumes.Where(volume => volume.Number > 16).ShouldAllBe(volume => !volume.IsReleased);
    }

    [Fact]
    public void Revert_VolumeCountAniList_FallsBackToTheRecordedCountWhenNoSyncHasRead()
    {
        var series = CreateSeries(volumeCount: 18, released: false);
        series.ReleaseSourceVolumeCount = null;
        var choice = Choice(SyncDiscrepancyField.VolumeCount, null, MetadataSourceName.AniList);
        _applier.Apply(series, choice, recorded: null);

        var row = new SyncDiscrepancy { Field = SyncDiscrepancyField.VolumeCount }
            .Says(MetadataSourceName.AniList, "18")
            .Says(MetadataSourceName.MangaUpdates, "16");

        _applier.Revert(series, choice, row);

        series.Volumes.Count(volume => volume.IsReleased).ShouldBe(16);
    }

    [Fact]
    public void Revert_VolumeCountAniList_WithNoCountAnywhere_LeavesTheVolumesAlone()
    {
        var series = CreateSeries(volumeCount: 4, released: false);
        series.ReleaseSourceVolumeCount = null;
        var choice = Choice(SyncDiscrepancyField.VolumeCount, null, MetadataSourceName.AniList);
        _applier.Apply(series, choice, recorded: null);

        _applier.Revert(series, choice, recorded: null);

        series.Volumes.ShouldAllBe(volume => volume.IsReleased);
    }

    [Fact]
    public void Revert_StatusMangaUpdates_RestoresTheReplacedValue()
    {
        var series = CreateSeries(status: PublicationStatus.Completed);
        var choice = Choice(SyncDiscrepancyField.Status, null, MetadataSourceName.MangaUpdates);
        _applier.Apply(series, choice, StatusRow("Completed", "Hiatus"));

        _applier.Revert(series, choice, StatusRow("Completed", "Hiatus"));

        series.PublicationStatus.ShouldBe(PublicationStatus.Completed);
    }

    [Fact]
    public async Task Revert_ThenRefresh_RecordsTheConflictAsUnresolvedAgain()
    {
        var series = CreateSeries();
        var choice = Choice(SyncDiscrepancyField.ReleaseDate, 8, MetadataSourceName.AppleBooks);
        series.SourceChoices.Add(choice);
        _applier.Apply(series, choice, DateRow(MetadataSourceName.AppleBooks, "2099-03-09"));

        // The reset: the effect goes, then the choice itself.
        _applier.Revert(series, choice, recorded: null);
        series.SourceChoices.Remove(choice);

        var run = await RefreshAsync(series, Info(), Catalogue(AppleRecord(FirstDate)));

        // Back to precedence: the count says out, the future date is refused and recorded for a
        // person, and nothing of the choice is left.
        VolumeEight(series).ReleaseDate.ShouldBeNull();
        var discrepancy = run.Discrepancies.ShouldHaveSingleItem();
        discrepancy.ResolvedWith.ShouldBeNull();
        discrepancy.ResolvedByChoice.ShouldBeFalse();
    }

    private static Volume VolumeEight(Series series) => series.Volumes.Single(volume => volume.Number is 8);

    private static SyncDiscrepancy DateRow(MetadataSourceName source, string value) =>
        new SyncDiscrepancy { Field = SyncDiscrepancyField.ReleaseDate, VolumeNumber = 8 }
            .Says(MetadataSourceName.MangaUpdates, "in print")
            .Says(source, value);

    private static SyncDiscrepancy StatusRow(string shelf, string source) =>
        new SyncDiscrepancy { Field = SyncDiscrepancyField.Status }
            .Says(MetadataSourceName.AniList, shelf)
            .Says(MetadataSourceName.MangaUpdates, source);
}
