using static LnShelf.Api.Tests.Features.Metadata.Services.SourceChoiceTestContext;
using static LnShelf.Api.Tests.Features.Metadata.Services.VolumeEnrichmentTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// What a refresh does once the user has picked a source on a disagreement: the choice comes
/// first for its one field, it survives every run, and it never touches the user's own state.
/// </summary>
public sealed class SourceChoiceSyncTests
{
    [Fact]
    public async Task RefreshAsync_ReleaseDateChoiceOfAppleBooks_WritesTheFutureDateAndKeepsTheVolumeNotOut()
    {
        var series = CreateSeries();
        series.SourceChoices.Add(Choice(SyncDiscrepancyField.ReleaseDate, 8, MetadataSourceName.AppleBooks));

        var run = await RefreshAsync(series, Info(), Catalogue(AppleRecord(FirstDate)));

        var volume = series.Volumes.Single(candidate => candidate.Number is 8);
        volume.ReleaseDate.ShouldBe(FirstDate);
        volume.ReleaseDateSource.ShouldBe(MetadataSourceName.AppleBooks);

        // The date decides whether the volume is out, so the count's word is withdrawn for
        // this one volume: the shelf must not call out a volume it dates in 2099.
        volume.IsReleased.ShouldBeFalse();
        series.Volumes.Where(candidate => candidate.Number < 8).ShouldAllBe(candidate => candidate.IsReleased);
        run.DatesMoved.ShouldBe(1);

        // Still recorded, as the user's: the disagreement did not go away.
        var discrepancy = run.Discrepancies.ShouldHaveSingleItem();
        discrepancy.Field.ShouldBe(SyncDiscrepancyField.ReleaseDate);
        discrepancy.VolumeNumber.ShouldBe(8);
        discrepancy.ResolvedWith.ShouldBe(MetadataSourceName.AppleBooks);
        discrepancy.ResolvedByChoice.ShouldBeTrue();
        discrepancy.Values.Single(value => value.Source is MetadataSourceName.MangaUpdates)
            .Value.ShouldBe("in print");
        discrepancy.Values.Single(value => value.Source is MetadataSourceName.AppleBooks)
            .Value.ShouldBe("2099-03-09");
    }

    [Fact]
    public async Task RefreshAsync_ReleaseDateChoiceOfAppleBooks_FollowsAMovedDate()
    {
        var series = CreateSeries();
        series.SourceChoices.Add(Choice(SyncDiscrepancyField.ReleaseDate, 8, MetadataSourceName.AppleBooks));
        await RefreshAsync(series, Info(), Catalogue(AppleRecord(FirstDate)));

        // The publisher slips the date a week. A frozen value would still say 9 March.
        var run = await RefreshAsync(series, Info(), Catalogue(AppleRecord(MovedDate)));

        series.Volumes.Single(candidate => candidate.Number is 8).ReleaseDate.ShouldBe(MovedDate);
        run.DatesMoved.ShouldBe(1);
    }

    [Fact]
    public async Task RefreshAsync_ReleaseDateChoiceOfAppleBooks_IgnoresOpenLibrarysDate()
    {
        var series = CreateSeries();
        series.SourceChoices.Add(Choice(SyncDiscrepancyField.ReleaseDate, 8, MetadataSourceName.AppleBooks));

        await RefreshAsync(
            series,
            Info(),
            Catalogue(AppleRecord(FirstDate)),
            LibraryEdition(LibraryDate));

        // Open Library outranks the store by default and may replace its date; the choice
        // outranks both.
        var volume = series.Volumes.Single(candidate => candidate.Number is 8);
        volume.ReleaseDate.ShouldBe(FirstDate);
        volume.ReleaseDateSource.ShouldBe(MetadataSourceName.AppleBooks);
    }

    [Fact]
    public async Task RefreshAsync_ReleaseDateChoiceOfMangaUpdates_WritesNothingAndRecordsResolvedByChoice()
    {
        var series = CreateSeries();
        series.SourceChoices.Add(Choice(SyncDiscrepancyField.ReleaseDate, 8, MetadataSourceName.MangaUpdates));

        var run = await RefreshAsync(series, Info(), Catalogue(AppleRecord(FirstDate)));

        var volume = series.Volumes.Single(candidate => candidate.Number is 8);
        volume.ReleaseDate.ShouldBeNull();
        volume.IsReleased.ShouldBeTrue();
        run.DatesMoved.ShouldBe(0);

        var discrepancy = run.Discrepancies.ShouldHaveSingleItem();
        discrepancy.ResolvedWith.ShouldBe(MetadataSourceName.MangaUpdates);
        discrepancy.ResolvedByChoice.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshAsync_VolumeCountChoiceOfAniList_MarksEverySyncedVolumeOutAndCreatesNone()
    {
        // The Overlord shape, made stronger: the shelf tracks 18 and none is out yet.
        var series = CreateSeries(volumeCount: 18, released: false);
        series.SourceChoices.Add(Choice(SyncDiscrepancyField.VolumeCount, null, MetadataSourceName.AniList));

        var run = await RefreshAsync(series, Info(volumeCount: 16));

        series.Volumes.Count.ShouldBe(18);
        series.Volumes.ShouldAllBe(volume => volume.IsReleased);
        run.NewVolumes.ShouldBe(0);
        series.ReleaseSourceVolumeCount.ShouldBe(16);

        var discrepancy = run.Discrepancies.ShouldHaveSingleItem();
        discrepancy.Field.ShouldBe(SyncDiscrepancyField.VolumeCount);
        discrepancy.ResolvedWith.ShouldBe(MetadataSourceName.AniList);
        discrepancy.ResolvedByChoice.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshAsync_VolumeCountChoiceOfAniList_LeavesLocalOnlyVolumesAlone()
    {
        var series = CreateSeries(volumeCount: 4, released: false);
        series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 5, IsLocalOnly = true });
        series.SourceChoices.Add(Choice(SyncDiscrepancyField.VolumeCount, null, MetadataSourceName.AniList));

        await RefreshAsync(series, Info(volumeCount: 3));

        // A volume the user added by hand is not something the shelf's AniList list says is
        // in print.
        series.Volumes.Where(volume => !volume.IsLocalOnly).ShouldAllBe(volume => volume.IsReleased);
        series.Volumes.Single(volume => volume.IsLocalOnly).IsReleased.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshAsync_StatusChoiceOfMangaUpdates_WritesTheMappedStatusEveryRun()
    {
        var series = CreateSeries(status: PublicationStatus.Completed);
        series.SourceChoices.Add(Choice(
            SyncDiscrepancyField.Status, null, MetadataSourceName.MangaUpdates, replacedValue: "Completed"));

        var hiatus = await RefreshAsync(series, Info(status: PublicationStatus.Hiatus));

        series.PublicationStatus.ShouldBe(PublicationStatus.Hiatus);

        var discrepancy = hiatus.Discrepancies.ShouldHaveSingleItem();
        discrepancy.Field.ShouldBe(SyncDiscrepancyField.Status);
        discrepancy.ResolvedWith.ShouldBe(MetadataSourceName.MangaUpdates);
        discrepancy.ResolvedByChoice.ShouldBeTrue();
        discrepancy.Values.Single(value => value.Source is MetadataSourceName.AniList).Value.ShouldBe("Completed");
        discrepancy.Values.Single(value => value.Source is MetadataSourceName.MangaUpdates).Value.ShouldBe("Hiatus");

        // Later the series finishes, and the shelf follows the source it was told to follow.
        var completed = await RefreshAsync(series, Info(status: PublicationStatus.Completed));

        series.PublicationStatus.ShouldBe(PublicationStatus.Completed);
        completed.Discrepancies.ShouldBeEmpty();
    }

    [Fact]
    public async Task RefreshAsync_StatusChoiceOfAniList_KeepsCompleted()
    {
        var series = CreateSeries(status: PublicationStatus.Completed);
        series.SourceChoices.Add(Choice(SyncDiscrepancyField.Status, null, MetadataSourceName.AniList));

        var run = await RefreshAsync(series, Info(status: PublicationStatus.Ongoing));

        series.PublicationStatus.ShouldBe(PublicationStatus.Completed);

        var discrepancy = run.Discrepancies.ShouldHaveSingleItem();
        discrepancy.ResolvedWith.ShouldBe(MetadataSourceName.AniList);
        discrepancy.ResolvedByChoice.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(EveryChoice))]
    public async Task RefreshAsync_AnyChoice_SurvivesTheRefreshUnchanged(ChoiceScenario scenario)
    {
        var series = scenario.Build();
        var before = series.SourceChoices.Single();
        var snapshot = (before.Id, before.Field, before.VolumeNumber, before.Source, before.ReplacedValue, before.ChosenAt);

        await RefreshAsync(series, scenario.ReleaseInfo, scenario.CatalogueSource, scenario.EnrichmentSource);

        // Sync reads a choice and never writes, replaces or deletes it (CLAUDE.md).
        var after = series.SourceChoices.ShouldHaveSingleItem();
        (after.Id, after.Field, after.VolumeNumber, after.Source, after.ReplacedValue, after.ChosenAt)
            .ShouldBe(snapshot);
    }

    [Theory]
    [MemberData(nameof(EveryChoice))]
    public async Task RefreshAsync_AnyChoice_NeverTouchesUserStateOrRaisesTheBadge(ChoiceScenario scenario)
    {
        var series = scenario.Build();
        series.UserState!.Notes = "Waiting on the omnibus.";
        var volumeState = new VolumeUserState { VolumeId = series.Volumes.First().Id, Owned = true, ReadStatus = ReadStatus.Read };
        series.Volumes.First().UserState = volumeState;

        await RefreshAsync(series, scenario.ReleaseInfo, scenario.CatalogueSource, scenario.EnrichmentSource);

        // The sanctioned HasNewVolume exception stays unused: nothing became newly out.
        series.UserState.HasNewVolume.ShouldBeFalse();
        series.UserState.TrackingStatus.ShouldBe(TrackingStatus.Reading);
        series.UserState.Notes.ShouldBe("Waiting on the omnibus.");
        volumeState.Owned.ShouldBeTrue();
        volumeState.ReadStatus.ShouldBe(ReadStatus.Read);
    }

    [Theory]
    [MemberData(nameof(EveryChoice))]
    public async Task RefreshAsync_TwiceWithAChoice_SecondRunChangesNothing(ChoiceScenario scenario)
    {
        var series = scenario.Build();

        await RefreshAsync(series, scenario.ReleaseInfo, scenario.CatalogueSource, scenario.EnrichmentSource);
        var afterFirst = Snapshot(series);

        var second = await RefreshAsync(series, scenario.ReleaseInfo, scenario.CatalogueSource, scenario.EnrichmentSource);

        // Idempotency (testing.md #3): the same sync twice changes nothing.
        Snapshot(series).ShouldBe(afterFirst);
        second.DatesMoved.ShouldBe(0);
        second.VolumesMarkedReleased.ShouldBe(0);
        second.NewVolumes.ShouldBe(0);
    }

    [Fact]
    public async Task RefreshAsync_ChosenSourceNowAgrees_RecordsNothingAndKeepsTheChoice()
    {
        var series = CreateSeries();
        var agreedDate = new DateOnly(2020, 1, 1);
        var volume = series.Volumes.Single(candidate => candidate.Number is 8);
        volume.ReleaseDate = agreedDate;
        volume.ReleaseDateSource = MetadataSourceName.AppleBooks;
        volume.IsReleased = false;
        series.SourceChoices.Add(Choice(SyncDiscrepancyField.ReleaseDate, 8, MetadataSourceName.AppleBooks));

        // The store's date has passed, so it now agrees with the count that the volume is out.
        var run = await RefreshAsync(series, Info(), Catalogue(AppleRecord(agreedDate)));

        run.Discrepancies.ShouldBeEmpty();
        series.SourceChoices.ShouldHaveSingleItem().Source.ShouldBe(MetadataSourceName.AppleBooks);
    }

    [Fact]
    public async Task RefreshAsync_ChosenSourceIsSilent_KeepsTheLastWrittenValue()
    {
        var series = CreateSeries();
        var volume = series.Volumes.Single(candidate => candidate.Number is 8);
        volume.ReleaseDate = FirstDate;
        volume.ReleaseDateSource = MetadataSourceName.AppleBooks;
        volume.IsReleased = false;
        series.SourceChoices.Add(Choice(SyncDiscrepancyField.ReleaseDate, 8, MetadataSourceName.AppleBooks));

        // The listing is gone. A source that says nothing writes nothing.
        await RefreshAsync(series, Info(), Catalogue());

        volume.ReleaseDate.ShouldBe(FirstDate);
        volume.IsReleased.ShouldBeFalse();
        series.SourceChoices.ShouldHaveSingleItem();
    }

    public static TheoryData<ChoiceScenario> EveryChoice() => new()
    {
        new(SyncDiscrepancyField.ReleaseDate, 8, MetadataSourceName.AppleBooks),
        new(SyncDiscrepancyField.ReleaseDate, 8, MetadataSourceName.OpenLibrary),
        new(SyncDiscrepancyField.ReleaseDate, 8, MetadataSourceName.MangaUpdates),
        new(SyncDiscrepancyField.VolumeCount, null, MetadataSourceName.AniList),
        new(SyncDiscrepancyField.VolumeCount, null, MetadataSourceName.MangaUpdates),
        new(SyncDiscrepancyField.Status, null, MetadataSourceName.AniList),
        new(SyncDiscrepancyField.Status, null, MetadataSourceName.MangaUpdates),
    };

    /// <summary>
    /// One choice in the state a series is in once the choice has been applied and a sync has
    /// settled: what the next run starts from.
    /// </summary>
    public sealed class ChoiceScenario(SyncDiscrepancyField choiceField, int? choiceVolumeNumber, MetadataSourceName choiceSource)
    {
        public ReleaseSeriesInfo ReleaseInfo { get; } = Info();

        public IVolumeCatalogueSource CatalogueSource => choiceField is SyncDiscrepancyField.ReleaseDate
            ? Catalogue(AppleRecord(FirstDate))
            : NoCatalogue();

        public IVolumeEnrichmentSource EnrichmentSource => choiceSource is MetadataSourceName.OpenLibrary
            ? LibraryEdition(FirstDate)
            : NoEnrichment();

        public Series Build()
        {
            var series = CreateSeries(
                status: choiceField is SyncDiscrepancyField.Status && choiceSource is MetadataSourceName.AniList
                    ? PublicationStatus.Completed
                    : PublicationStatus.Ongoing);

            var replacedValue = choiceField is SyncDiscrepancyField.Status && choiceSource is MetadataSourceName.MangaUpdates
                ? "Completed"
                : null;

            series.SourceChoices.Add(Choice(choiceField, choiceVolumeNumber, choiceSource, replacedValue));

            if (choiceField is SyncDiscrepancyField.ReleaseDate && choiceSource is not MetadataSourceName.MangaUpdates)
            {
                var volume = series.Volumes.Single(candidate => candidate.Number == choiceVolumeNumber);
                volume.ReleaseDate = FirstDate;
                volume.ReleaseDateSource = choiceSource;
                volume.IsReleased = false;
            }

            return series;
        }

        public override string ToString() => $"{choiceField} {choiceVolumeNumber} {choiceSource}";
    }

    private static string Snapshot(Series series) =>
        string.Join(
            "|",
            series.Volumes.OrderBy(volume => volume.Number).Select(volume =>
                $"{volume.Number}:{volume.ReleaseDate}:{volume.ReleaseDateSource}:{volume.IsReleased}"))
        + $"|{series.PublicationStatus}|{series.ReleaseSourceVolumeCount}";
}
