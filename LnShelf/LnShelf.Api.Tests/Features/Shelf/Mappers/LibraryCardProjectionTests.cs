namespace LnShelf.Api.Tests.Features.Shelf.Mappers;

/// <summary>
/// The derived-state rules are the thing the whole UI depends on, so they get the
/// full read/owned/released matrix (testing.md priority 1).
/// </summary>
// The tests compile the production expression and run it in memory: same rules,
// no database, and no second implementation to drift.
public sealed class LibraryCardProjectionTests
{
    private static readonly DateOnly Today = new(2026, 7, 29);
    private static readonly Func<Series, LibraryCardResponse> ToCard =
        LibraryCardProjection.ToCard(Today).Compile();

    /// <summary>
    /// Every combination of read status (null meaning no user-state row at all),
    /// ownership, and release timing (days from today, null meaning announced
    /// with no date).
    /// </summary>
    public static TheoryData<ReadStatus?, bool, int?, int, int, int, int, int, bool> VolumeStateMatrix => new()
    {
        // readStatus         owned  release  read  owned  published  toBuy  backlog  behind
        //
        // No user-state row: unread and unowned by absence.
        { null,               false, -1,      0,    0,     1,         1,     0,       true },
        { null,               false, 30,      0,    0,     0,         0,     0,       false },
        { null,               false, null,    0,    0,     0,         0,     0,       false },

        // Unread, not owned — released volumes are the to-buy pile.
        { ReadStatus.Unread,  false, -1,      0,    0,     1,         1,     0,       true },
        { ReadStatus.Unread,  false, 30,      0,    0,     0,         0,     0,       false },
        { ReadStatus.Unread,  false, null,    0,    0,     0,         0,     0,       false },

        // Unread, owned — backlog, whether or not it is even out yet
        // (a pre-order can be owned before release).
        { ReadStatus.Unread,  true,  -1,      0,    1,     1,         0,     1,       true },
        { ReadStatus.Unread,  true,  30,      0,    1,     0,         0,     1,       true },
        { ReadStatus.Unread,  true,  null,    0,    1,     0,         0,     1,       true },

        // Reading, not owned — borrowed and in progress; still to buy.
        { ReadStatus.Reading, false, -1,      0,    0,     1,         1,     0,       true },
        { ReadStatus.Reading, false, 30,      0,    0,     0,         0,     0,       false },
        { ReadStatus.Reading, false, null,    0,    0,     0,         0,     0,       false },

        // Reading, owned — in hand, not finished, so still backlog.
        { ReadStatus.Reading, true,  -1,      0,    1,     1,         0,     1,       true },
        { ReadStatus.Reading, true,  30,      0,    1,     0,         0,     1,       true },
        { ReadStatus.Reading, true,  null,    0,    1,     0,         0,     1,       true },

        // Read, not owned — read it, do not own it, so it is still to buy.
        { ReadStatus.Read,    false, -1,      1,    0,     1,         1,     0,       true },
        { ReadStatus.Read,    false, 30,      1,    0,     0,         0,     0,       false },
        { ReadStatus.Read,    false, null,    1,    0,     0,         0,     0,       false },

        // Read and owned — the only combination that is settled.
        { ReadStatus.Read,    true,  -1,      1,    1,     1,         0,     0,       false },
        { ReadStatus.Read,    true,  30,      1,    1,     0,         0,     0,       false },
        { ReadStatus.Read,    true,  null,    1,    1,     0,         0,     0,       false },
    };

    [Theory]
    [MemberData(nameof(VolumeStateMatrix))]
    public void ToCard_SingleVolumeInEveryState_ComputesDerivedCounts(
        ReadStatus? readStatus,
        bool owned,
        int? releaseDaysFromToday,
        int expectedRead,
        int expectedOwned,
        int expectedPublished,
        int expectedToBuy,
        int expectedBacklog,
        bool expectedBehind)
    {
        var series = CreateSeriesWithOneVolume(readStatus, owned, releaseDaysFromToday);

        var card = ToCard(series);

        card.Read.ShouldBe(expectedRead);
        card.Owned.ShouldBe(expectedOwned);
        card.Published.ShouldBe(expectedPublished);
        card.ToBuy.ShouldBe(expectedToBuy);
        card.Backlog.ShouldBe(expectedBacklog);
        card.Behind.ShouldBe(expectedBehind);
        card.Tracked.ShouldBe(1);
    }

    [Fact]
    public void ToCard_VolumeReleasedToday_CountsAsPublished()
    {
        var series = CreateSeriesWithOneVolume(ReadStatus.Unread, owned: false, releaseDaysFromToday: 0);

        var card = ToCard(series);

        card.Published.ShouldBe(1);
        card.ToBuy.ShouldBe(1);
    }

    [Fact]
    public void ToCard_SeededSeries_MatchesTheMockupsNumbers()
    {
        var card = ToCard(ShelfSeedData.CreateTanyaSeries());

        card.TitleEnglish.ShouldBe("The Saga of Tanya the Evil");
        card.Format.ShouldBe(Format.LightNovel);
        card.TrackingStatus.ShouldBe(TrackingStatus.Reading);
        card.HasNewVolume.ShouldBeTrue();

        card.Read.ShouldBe(8);
        card.Owned.ShouldBe(11);
        card.Published.ShouldBe(14);
        card.Tracked.ShouldBe(17);

        // 12, 13 and 14 are out and unbought; 9, 10 and 11 are bought and unread.
        card.ToBuy.ShouldBe(3);
        card.Backlog.ShouldBe(3);
        card.Behind.ShouldBeTrue();
        card.NextToBuyNumber.ShouldBe(12);
    }

    [Fact]
    public void ToCard_LowestUnownedReleasedVolume_IsTheNextToBuy()
    {
        var series = CreateSeriesWithOneVolume(ReadStatus.Read, owned: true, releaseDaysFromToday: -100);
        series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 3, ReleaseDate = Today.AddDays(-2) });
        series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 2, ReleaseDate = Today.AddDays(-40) });

        var card = ToCard(series);

        card.NextToBuyNumber.ShouldBe(2);
        card.ToBuy.ShouldBe(2);
    }

    [Fact]
    public void ToCard_CaughtUpSeries_HasNoNextToBuy()
    {
        var series = CreateSeriesWithOneVolume(ReadStatus.Read, owned: true, releaseDaysFromToday: -10);

        var card = ToCard(series);

        card.Behind.ShouldBeFalse();
        card.NextToBuyNumber.ShouldBeNull();
    }

    [Fact]
    public void ToCard_SeriesWithNoUserState_IsNotTrackedYet()
    {
        var series = CreateSeriesWithOneVolume(ReadStatus.Unread, owned: false, releaseDaysFromToday: -1);
        series.UserState = null;

        var card = ToCard(series);

        card.TrackingStatus.ShouldBe(TrackingStatus.PlanToStart);
        card.HasNewVolume.ShouldBeFalse();
    }

    /// <param name="readStatus">Null creates no volume-user-state row at all.</param>
    /// <param name="releaseDaysFromToday">Null leaves the volume announced with no date.</param>
    private static Series CreateSeriesWithOneVolume(ReadStatus? readStatus, bool owned, int? releaseDaysFromToday)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            TitleEnglish = "Test Series",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
            UserState = new SeriesUserState { TrackingStatus = TrackingStatus.Reading },
        };

        series.Volumes.Add(new Volume
        {
            Id = Guid.CreateVersion7(),
            Number = 1,
            ReleaseDate = releaseDaysFromToday is int days ? Today.AddDays(days) : null,
            UserState = readStatus is ReadStatus status
                ? new VolumeUserState { ReadStatus = status, Owned = owned }
                : null,
        });

        return series;
    }
}
