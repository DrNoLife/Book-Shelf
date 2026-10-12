using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// Which of a store's records are this series' volumes — the one walk that answers both
/// questions asked of a catalogue answer: the record per tracked volume, and the highest
/// volume number the store confirms at all.
/// </summary>
public sealed class CatalogueVolumeMatcherTests
{
    [Fact]
    public void Match_AStoreAnswer_KeepsOnlyTheRecordsThatAreThisSeriesVolumes()
    {
        var match = Match(
            Series(1, 2),
            Record("Overlord, Vol. 1 (light Novel)"),
            Record("Overlord, Vol. 2 (light Novel)"),
            Record("Overlord's Unfinished Business, Vol. 1"),
            Record("That Time I Got Reincarnated as a Slime, Vol. 2 (light Novel)"));

        match.ByVolumeNumber.Keys.Order().ShouldBe([1, 2]);
    }

    [Fact]
    public void Match_ARecordThatDoesNotStateTheMedium_IsRefused()
    {
        // The bar the enrichment pass writes behind: a record has to say which medium it is,
        // not merely fail to say it is the other one.
        var match = Match(Series(1), Record("Overlord 1"));

        match.ByVolumeNumber.ShouldBeEmpty();
        match.HighestConfirmedNumber.ShouldBe(0);
    }

    [Fact]
    public void Match_AVolumeListedTwice_KeepsTheFirstConfirmedRecord()
    {
        // Apple lists a volume more than once — a bundle, a re-release — and the store's own
        // ranking puts the plain edition first.
        var match = Match(
            Series(1),
            Record("Overlord, Vol. 1 (light Novel)", "first"),
            Record("Overlord, Vol. 1 (light Novel)", "second"));

        match.ByVolumeNumber[1].SourceRecordKey.ShouldBe("first");
    }

    [Fact]
    public void Match_ABonusVolumeListedBeforeTheRealOne_DoesNotTakeItsPlace()
    {
        // The Torture Princess case: Apple carries "Vol. 7.5" beside "Vol. 7", and while the
        // fraction was read as volume 7 whichever came first won the real volume's cover.
        var match = Match(
            Series(7),
            Record("Overlord, Vol. 7.5 (light Novel)", "bonus"),
            Record("Overlord, Vol. 7 (light Novel)", "real"));

        match.ByVolumeNumber[7].SourceRecordKey.ShouldBe("real");
    }

    [Fact]
    public void Match_ABonusVolume_IsKeyedByItsOwnNumberAndDoesNotRaiseTheCount()
    {
        // Vol. 7.5 is a book of its own, but not a tenth volume of a nine-volume series: the
        // highest confirmed number answers "how many main volumes", which it is not one of.
        var series = Series(7);
        series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 7.5m });

        var match = Match(
            series,
            Record("Overlord, Vol. 7 (light Novel)", "real"),
            Record("Overlord, Vol. 7.5 (light Novel)", "bonus"));

        match.ByVolumeNumber[7.5m].SourceRecordKey.ShouldBe("bonus");
        match.HighestConfirmedNumber.ShouldBe(7);
    }

    [Fact]
    public void Match_AVolumeNumberTheShelfDoesNotTrack_IsIgnoredButStillCounted()
    {
        // The release source owns volume counts, so a store listing volume 5 of a series the
        // count says has 2 is not grounds to create three volumes. It is still evidence of
        // what the store carries, which is the only answer left when no source states a
        // count at all.
        var match = Match(
            Series(1, 2),
            Record("Overlord, Vol. 2 (light Novel)"),
            Record("Overlord, Vol. 5 (light Novel)"));

        match.ByVolumeNumber.Keys.ShouldBe([2]);
        match.HighestConfirmedNumber.ShouldBe(5);
    }

    [Fact]
    public void Match_ASeriesWithNoVolumesYet_StillReportsTheHighestConfirmedNumber()
    {
        // The freshly resolved series the verification command builds: it has no volumes
        // until this number says how many to add.
        var match = Match(
            Series(),
            Record("Overlord, Vol. 3 (light Novel)"),
            Record("Overlord 9"),
            Record("Overlord, Vol. 7 (light Novel)"));

        match.ByVolumeNumber.ShouldBeEmpty();
        match.HighestConfirmedNumber.ShouldBe(7);
    }

    [Fact]
    public void Match_AnAnswerFetchedUnderAnAlternativeTitle_IsMatchedAgainstThatTitle()
    {
        // The store answered under a name the shelf does not use, so its records name that
        // one and the shelf's own appears nowhere in them.
        var series = Series(3);
        series.TitleEnglish = "Kono Subarashii Sekai ni Shukufuku o!";

        var matcher = new CatalogueVolumeMatcher(NullLogger<CatalogueVolumeMatcher>.Instance);

        var match = matcher.Match(
            series,
            "Konosuba: God's Blessing on This Wonderful World!",
            [Record("Konosuba: God's Blessing on This Wonderful World!, Vol. 3 (light novel)")]);

        match.ByVolumeNumber.Keys.ShouldBe([3]);
    }

    [Fact]
    public void Match_ComicMarkedVolumes_AreTheMangasAndThoseNotTrackedAreKeptAside()
    {
        // The captured Solo Leveling answer against the shelf the owner has: thirteen volumes,
        // because MangaUpdates counts "13+2".
        var series = Series("Solo Leveling", Format.Manga, Enumerable.Range(1, 13).ToArray());

        var match = Match(series, [.. AppleBooksFixtures.LoadCatalogue(AppleBooksFixtures.SoloLevelingSearch)]);

        match.ByVolumeNumber.Keys.Order().ShouldBe(Enumerable.Range(1, 13).Select(number => (decimal)number));

        // 14 and 15 are confirmed but not tracked: candidates, not volumes. The Ragnarok sequel
        // and the "(novel)" light novel are neither.
        match.UntrackedMainVolumes.Keys.Order().ShouldBe([14m, 15m]);
        match.UntrackedMainVolumes[15].Title.ShouldBe("Solo Leveling, Vol. 15 (comic)");
        match.HighestConfirmedNumber.ShouldBe(15);
    }

    [Fact]
    public void Match_UnmarkedVizTitles_AreConfirmedByTheStoresMangaGenre()
    {
        var series = Series("Chainsaw Man", Format.Manga, Enumerable.Range(1, 22).ToArray());

        var match = Match(series, [.. AppleBooksFixtures.LoadCatalogue(AppleBooksFixtures.ChainsawManSearch)]);

        match.ByVolumeNumber.Keys.Order().ShouldBe([1m, 22m]);
        match.UntrackedMainVolumes.Keys.ShouldBe([23m]);
    }

    [Fact]
    public void Match_LightNovelRowsFiledUnderManga_ConfirmNothingForALightNovel()
    {
        // Apple files Bookworm's novels under "Manga". For a light novel the genre is not
        // evidence at all, and the title says no medium, so the row is refused as before.
        var series = Series("Ascendance of a Bookworm: Part 4", Format.LightNovel, 5);

        var match = Match(series, [.. AppleBooksFixtures.LoadCatalogue(AppleBooksFixtures.BookwormSearch)]);

        match.ByVolumeNumber.ShouldBeEmpty();
        match.HighestConfirmedNumber.ShouldBe(0);
    }

    [Fact]
    public void Match_TheMediumBeforeTheVolumeMarker_IsMatchedForItsOwnMediumOnly()
    {
        var series = Series("Classroom of the Elite", Format.LightNovel, [.. Enumerable.Range(1, 11)]);

        var match = Match(series, [.. AppleBooksFixtures.LoadCatalogue(AppleBooksFixtures.ClassroomOfTheEliteSearch)]);

        // "(Light Novel) Vol. 1" is volume 1; "(Manga) Vol. 1" one row away and the "Year 2"
        // sequel are not.
        match.ByVolumeNumber.Keys.ShouldBe([1m]);
        match.ByVolumeNumber[1].Title.ShouldBe("Classroom of the Elite (Light Novel) Vol. 1");
        match.BonusVolumes.Keys.ShouldBe([11.5m]);
    }

    private static CatalogueMatch Match(Series series, params CatalogueVolume[] catalogue) =>
        new CatalogueVolumeMatcher(NullLogger<CatalogueVolumeMatcher>.Instance)
            .Match(series, series.TitleEnglish, catalogue);

    private static CatalogueVolume Record(string title, string sourceRecordKey = "1560603224") => new()
    {
        SourceRecordKey = sourceRecordKey,
        Title = title,
    };

    private static Series Series(params int[] volumeNumbers) =>
        Series("Overlord", Format.LightNovel, volumeNumbers);

    private static Series Series(string title, Format format, params int[] volumeNumbers)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 85976,
            TitleEnglish = title,
            Format = format,
            PublicationStatus = PublicationStatus.Ongoing,
        };

        foreach (var number in volumeNumbers)
        {
            series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = number });
        }

        return series;
    }
}
