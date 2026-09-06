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

    private static CatalogueMatch Match(Series series, params CatalogueVolume[] catalogue) =>
        new CatalogueVolumeMatcher(NullLogger<CatalogueVolumeMatcher>.Instance)
            .Match(series, series.TitleEnglish, catalogue);

    private static CatalogueVolume Record(string title, string sourceRecordKey = "1560603224") => new()
    {
        SourceRecordKey = sourceRecordKey,
        Title = title,
    };

    private static Series Series(params int[] volumeNumbers)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 85976,
            TitleEnglish = "Overlord",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
        };

        foreach (var number in volumeNumbers)
        {
            series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = number });
        }

        return series;
    }
}
