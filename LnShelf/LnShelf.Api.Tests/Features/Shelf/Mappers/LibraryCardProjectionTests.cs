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

        // Unread, owned — backlog, and published whatever the date says, because a
        // volume in your hands is out. The last two rows are the ones an imported
        // library is made of: 74 owned volumes with no date anywhere, which used to
        // report published=0 and gave every progress bar a width of nothing.
        { ReadStatus.Unread,  true,  -1,      0,    1,     1,         0,     1,       true },
        { ReadStatus.Unread,  true,  30,      0,    1,     1,         0,     1,       true },
        { ReadStatus.Unread,  true,  null,    0,    1,     1,         0,     1,       true },

        // Reading, not owned — borrowed and in progress; still to buy.
        { ReadStatus.Reading, false, -1,      0,    0,     1,         1,     0,       true },
        { ReadStatus.Reading, false, 30,      0,    0,     0,         0,     0,       false },
        { ReadStatus.Reading, false, null,    0,    0,     0,         0,     0,       false },

        // Reading, owned — in hand, not finished, so still backlog.
        { ReadStatus.Reading, true,  -1,      0,    1,     1,         0,     1,       true },
        { ReadStatus.Reading, true,  30,      0,    1,     1,         0,     1,       true },
        { ReadStatus.Reading, true,  null,    0,    1,     1,         0,     1,       true },

        // Read, not owned — read it, do not own it, so it is still to buy.
        { ReadStatus.Read,    false, -1,      1,    0,     1,         1,     0,       true },
        { ReadStatus.Read,    false, 30,      1,    0,     0,         0,     0,       false },
        { ReadStatus.Read,    false, null,    1,    0,     0,         0,     0,       false },

        // Read and owned — the only combination that is settled, and published on
        // every row for the same reason: you cannot have read a volume you do not
        // have.
        { ReadStatus.Read,    true,  -1,      1,    1,     1,         0,     0,       false },
        { ReadStatus.Read,    true,  30,      1,    1,     1,         0,     0,       false },
        { ReadStatus.Read,    true,  null,    1,    1,     1,         0,     0,       false },
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

    /// <summary>
    /// Volume 1 is read, 2 is not owned, 3 is being read, 4 is owned and unread — so the next
    /// to read is 3: owned, not read, and "Reading" still counts, the same as Backlog.
    /// </summary>
    [Fact]
    public void ToCard_LowestOwnedVolumeNotRead_IsTheNextToRead()
    {
        var series = CreateSeriesWithOneVolume(ReadStatus.Read, owned: true, releaseDaysFromToday: -100);
        var reading = new Volume
        {
            Id = Guid.CreateVersion7(),
            Number = 3,
            IsReleased = true,
            UserState = new VolumeUserState { ReadStatus = ReadStatus.Reading, Owned = true },
        };
        series.Volumes.Add(new Volume
        {
            Id = Guid.CreateVersion7(),
            Number = 4,
            IsReleased = true,
            UserState = new VolumeUserState { ReadStatus = ReadStatus.Unread, Owned = true },
        });
        series.Volumes.Add(reading);
        series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 2, IsReleased = true });

        var card = ToCard(series);

        card.NextToReadNumber.ShouldBe(3);
        card.NextToReadVolumeId.ShouldBe(reading.Id);
    }

    [Theory]
    [InlineData(ReadStatus.Read, true)]
    [InlineData(ReadStatus.Unread, false)]
    [InlineData(ReadStatus.Reading, false)]
    public void ToCard_NothingOwnedAndUnread_HasNoNextToRead(ReadStatus readStatus, bool owned)
    {
        var series = CreateSeriesWithOneVolume(readStatus, owned, releaseDaysFromToday: -10);

        var card = ToCard(series);

        card.NextToReadNumber.ShouldBeNull();
        card.NextToReadVolumeId.ShouldBeNull();
        card.Backlog.ShouldBe(0);
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
    [Theory]
    [MemberData(nameof(VolumeStateMatrix))]
    public void ToCard_AnyVolumeState_NeverReportsFewerPublishedThanOwned(
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
        // The invariant the three-segment bar depends on. Its unowned segment is
        // Published - Owned, and a negative one made the whole bar span nothing;
        // the clamp in the component hid that rather than fixing it. Enforced here
        // instead, across every combination the matrix covers.
        var card = ToCard(CreateSeriesWithOneVolume(readStatus, owned, releaseDaysFromToday));

        // Published >= Owned only. Owned >= Read is deliberately *not* asserted: the
        // matrix has read-but-not-owned rows on purpose, because a borrowed volume is
        // a real thing.
        card.Published.ShouldBeGreaterThanOrEqualTo(card.Owned);

        // Named so xUnit does not warn about unused theory parameters, and asserted
        // so this test fails loudly if the matrix and the expectations drift apart.
        (expectedRead, expectedOwned, expectedPublished, expectedToBuy, expectedBacklog, expectedBehind)
            .ShouldBe((card.Read, card.Owned, card.Published, card.ToBuy, card.Backlog, card.Behind));
    }

    /// <summary>
    /// The third ground for "published": the release source says a volume is in
    /// print, without saying when. This is the only thing that makes a to-buy pile
    /// possible, because no source publishes per-volume dates.
    /// </summary>
    public static TheoryData<bool, bool, int, int, int, bool> ReleasedWithoutDateMatrix => new()
    {
        // isReleased  owned  published  toBuy  backlog  behind
        //
        // Confirmed in print and not owned: this is the to-buy case, and it cannot
        // be expressed by a date because there is no date to be had.
        { true,        false, 1,         1,     0,       true },

        // Confirmed in print and owned: published on two grounds, to buy on neither.
        { true,        true,  1,         0,     1,       true },

        // Not confirmed and not owned: nothing is known, so nothing is claimed.
        { false,       false, 0,         0,     0,       false },

        // Not confirmed but owned: owning it is its own proof of publication.
        { false,       true,  1,         0,     1,       true },
    };

    [Theory]
    [MemberData(nameof(ReleasedWithoutDateMatrix))]
    public void ToCard_AVolumeWithNoDate_UsesTheReleaseSourcesWord(
        bool isReleased,
        bool owned,
        int expectedPublished,
        int expectedToBuy,
        int expectedBacklog,
        bool expectedBehind)
    {
        var series = CreateSeriesWithOneVolume(
            owned ? ReadStatus.Unread : null, owned, releaseDaysFromToday: null);
        series.Volumes.Single().IsReleased = isReleased;

        var card = ToCard(series);

        card.Published.ShouldBe(expectedPublished);
        card.ToBuy.ShouldBe(expectedToBuy);
        card.Backlog.ShouldBe(expectedBacklog);
        card.Behind.ShouldBe(expectedBehind);
        card.Published.ShouldBeGreaterThanOrEqualTo(card.Owned);
    }

    /// <summary>
    /// The case per-volume enrichment introduced: a volume carrying <em>both</em> a real
    /// date and the release source's in-print flag.
    /// </summary>
    // Untested before the enrichment phase, and it is the case with teeth: the formula is
    // disjunctive, so any one ground is enough, and a future date can now sit beside a
    // flag that says the volume is already out. Which of the two wins is settled by the
    // sync (a future date on an in-print volume is recorded as a disagreement and not
    // written); what these rows pin down is that the projection's own answer never
    // changed — the flag still counts, whatever date is beside it.
    public static TheoryData<int, bool, int, int, bool> ReleasedWithADateMatrix => new()
    {
        // daysFromToday  owned  published  toBuy  isOut
        //
        // Dated in the past and in print: the two grounds agree, and it is out.
        { -30,            false, 1,         1,     true },
        { -30,            true,  1,         0,     true },

        // Out today. The comparison is <=, so the day itself counts as out.
        { 0,              false, 1,         1,     true },

        // Dated in the future and in print: the sources disagree, and the flag still
        // decides. Anything else would make the count-based release model unreliable on
        // exactly the 63% of volumes enrichment can reach.
        { 120,            false, 1,         1,     true },
        { 120,            true,  1,         0,     true },
    };

    [Theory]
    [MemberData(nameof(ReleasedWithADateMatrix))]
    public void ToCard_AVolumeThatIsBothDatedAndInPrint_StillCountsOnEitherGround(
        int daysFromToday,
        bool owned,
        int expectedPublished,
        int expectedToBuy,
        bool expectedIsOut)
    {
        var series = CreateSeriesWithOneVolume(
            owned ? ReadStatus.Unread : null, owned, releaseDaysFromToday: daysFromToday);
        series.Volumes.Single().IsReleased = true;

        var card = ToCard(series);

        // Call sites one and two: Published and ToBuy.
        card.Published.ShouldBe(expectedPublished);
        card.ToBuy.ShouldBe(expectedToBuy);

        // Call site three: the tick strip's own IsOut.
        card.VolumeTicks.ShouldHaveSingleItem().IsOut.ShouldBe(expectedIsOut);

        // Call site four, asserted here so the two projections cannot drift: the same
        // volume through the series-detail path.
        SeriesDetailProjection.ToVolume(series.Volumes.Single(), Today).IsOut.ShouldBe(expectedIsOut);
    }

    [Fact]
    public void ToCard_AFutureDatedVolumeThatIsInPrint_IsStillTheNextReleaseDate()
    {
        // The disagreement is the sync's problem, not the rows view's. If a date is on the
        // volume, the column that prints the soonest date ahead still prints it.
        var series = CreateSeriesWithOneVolume(readStatus: null, owned: false, releaseDaysFromToday: 45);
        series.Volumes.Single().IsReleased = true;

        var card = ToCard(series);

        card.NextReleaseDate.ShouldBe(Today.AddDays(45));

        // And it is simultaneously to buy, because IsReleased says so. Both are true, and
        // the sync footer is where that contradiction is reported.
        card.ToBuy.ShouldBe(1);
    }

    [Fact]
    public void ToCard_VolumesBeyondTheReleaseSourcesCount_AreNotToBuy()
    {
        // The real Overlord shape: AniList tracks 18 volumes, MangaUpdates counts 16,
        // 15 are owned. Volume 16 is to buy; 17 and 18 are not out yet.
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            TitleEnglish = "Overlord",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
            UserState = new SeriesUserState { TrackingStatus = TrackingStatus.Reading },
        };

        foreach (var number in Enumerable.Range(1, 18))
        {
            series.Volumes.Add(new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = number,
                ReleaseDate = null,
                IsReleased = number <= 16,
                UserState = number <= 15
                    ? new VolumeUserState { ReadStatus = ReadStatus.Read, Owned = true }
                    : null,
            });
        }

        var card = ToCard(series);

        card.Owned.ShouldBe(15);
        card.Published.ShouldBe(16);
        card.ToBuy.ShouldBe(1);
        card.NextToBuyNumber.ShouldBe(16);
        card.Tracked.ShouldBe(18);
        card.Behind.ShouldBeTrue();
    }

    [Fact]
    public void ToCard_AnImportedSeriesWithNoDatesAtAll_IsFullyPublished()
    {
        // The real shape of an imported library: every volume owned and read, not one
        // release date anywhere, because neither AniList nor MangaUpdates publishes
        // per-volume dates.
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            TitleEnglish = "Imported Series",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Completed,
            UserState = new SeriesUserState { TrackingStatus = TrackingStatus.Reading },
        };

        foreach (var number in Enumerable.Range(1, 9))
        {
            series.Volumes.Add(new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = number,
                ReleaseDate = null,
                UserState = new VolumeUserState { ReadStatus = ReadStatus.Read, Owned = true },
            });
        }

        var card = ToCard(series);

        card.Read.ShouldBe(9);
        card.Owned.ShouldBe(9);
        card.Published.ShouldBe(9);
        card.ToBuy.ShouldBe(0);
        card.Backlog.ShouldBe(0);
        card.Behind.ShouldBeFalse();
    }

    /// <summary>
    /// The rows view's tick strip is drawn from these, so they have to say the same
    /// thing the counts do — one tick per volume, in number order, no gaps.
    /// </summary>
    [Fact]
    public void ToCard_VolumeTicks_AgreeWithTheCountsAndAreInNumberOrder()
    {
        // The real Overlord shape again: 18 tracked, 16 in print, 16 owned, 15 read.
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            TitleEnglish = "Overlord",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
            UserState = new SeriesUserState { TrackingStatus = TrackingStatus.Reading },
        };

        // Added out of order on purpose: the strip reads left to right as volume 1
        // onwards, whatever order the rows arrive in.
        foreach (var number in (int[])[18, 3, 1, 2, 17, 16, .. Enumerable.Range(4, 12)])
        {
            series.Volumes.Add(new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = number,
                IsReleased = number <= 16,
                UserState = number <= 16
                    ? new VolumeUserState
                    {
                        ReadStatus = number <= 15 ? ReadStatus.Read : ReadStatus.Unread,
                        Owned = true,
                    }
                    : null,
            });
        }

        var card = ToCard(series);

        card.VolumeTicks.Count.ShouldBe(card.Tracked);
        card.VolumeTicks.Select(tick => (int)tick.Number).ShouldBe(Enumerable.Range(1, 18));

        card.VolumeTicks.Count(tick => tick.Kind is VolumeTickKind.Read).ShouldBe(card.Read);
        card.VolumeTicks.Count(tick => tick.Kind is VolumeTickKind.OwnedUnread).ShouldBe(1);
        card.VolumeTicks.Count(tick => tick.Kind is VolumeTickKind.ToBuy).ShouldBe(card.ToBuy);
        card.VolumeTicks.Count(tick => tick.Kind is VolumeTickKind.NotOut)
            .ShouldBe(card.Tracked - card.Published);
    }

    [Theory]
    [MemberData(nameof(VolumeStateMatrix))]
    public void ToCard_EveryVolumeState_ProducesExactlyOneTickThatMatchesThePublishedCount(
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
        var card = ToCard(CreateSeriesWithOneVolume(readStatus, owned, releaseDaysFromToday));
        var tick = card.VolumeTicks.ShouldHaveSingleItem();

        // The tick's own IsOut is the card's Published, for the same volume: the strip
        // cannot show a volume as not-out while the counts call it published.
        tick.IsOut.ShouldBe(expectedPublished == 1);

        // The precedence, written out because it surprised this test into failing:
        // "not out" outranks "read". A volume marked read that no source says is in
        // print is drawn hollow, which is exactly what the volume tile does with the
        // same volume (VolumeTileStateMap checks IsOut before read status). A strip and
        // a tile disagreeing about one volume would be the worse bug.
        tick.Kind.ShouldBe((expectedPublished, expectedRead, expectedOwned) switch
        {
            (0, _, _) => VolumeTickKind.NotOut,
            (_, 1, _) => VolumeTickKind.Read,
            (_, _, 1) => VolumeTickKind.OwnedUnread,
            _ => VolumeTickKind.ToBuy,
        });

        (expectedOwned, expectedBacklog, expectedBehind, expectedToBuy)
            .ShouldBe((card.Owned, card.Backlog, card.Behind, card.ToBuy));
    }

    [Fact]
    public void ToCard_AVolumeReadBeforeItIsOut_DrawsAsNotOutBecauseThatIsWhatTheTileDoes()
    {
        // A fan translation read ahead of the English release is the real case. The
        // counts say read=1, published=0; the tick says "the shelf does not have this
        // yet", which is the more useful thing for a strip to say and is what the tile
        // already says about the same volume.
        var series = CreateSeriesWithOneVolume(ReadStatus.Read, owned: false, releaseDaysFromToday: 30);

        var card = ToCard(series);

        card.Read.ShouldBe(1);
        card.Published.ShouldBe(0);
        card.VolumeTicks.ShouldHaveSingleItem().Kind.ShouldBe(VolumeTickKind.NotOut);
    }

    /// <summary>
    /// The rows view's next-volume column: the soonest date still ahead of us, and
    /// nothing at all when no volume carries one.
    /// </summary>
    [Fact]
    public void ToCard_NextReleaseDate_IsTheEarliestDateStillAhead()
    {
        var series = CreateSeriesWithOneVolume(ReadStatus.Read, owned: true, releaseDaysFromToday: -30);
        series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 4, ReleaseDate = Today.AddDays(120) });
        series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 3, ReleaseDate = Today.AddDays(45) });
        series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 5, ReleaseDate = null });

        var card = ToCard(series);

        card.NextReleaseDate.ShouldBe(Today.AddDays(45));
    }

    [Fact]
    public void ToCard_NothingDated_HasNoNextReleaseDate()
    {
        // The whole shelf's shape today: dates exist nowhere, so the column falls
        // through to "No date".
        var series = CreateSeriesWithOneVolume(ReadStatus.Read, owned: true, releaseDaysFromToday: null);
        series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 2, ReleaseDate = null });

        ToCard(series).NextReleaseDate.ShouldBeNull();
    }

    /// <summary>
    /// design.md: the card shows the latest volume's cover — the highest-numbered volume
    /// that actually has art, so a series tracking volumes enrichment has never reached
    /// still shows the newest cover it has rather than a placeholder.
    /// </summary>
    [Fact]
    public void ToCard_VolumesWithArt_ShowsTheHighestNumberedOne()
    {
        var series = CreateSeriesWithCovers(
            (1, null, null), (2, "volume/openlibrary/b.jpg", null), (3, null, null));

        ToCard(series).CoverObjectKey.ShouldBe("volume/openlibrary/b.jpg");
    }

    [Fact]
    public void ToCard_AVolumeCoverOverride_WinsOverThatVolumesSyncedArt()
    {
        // Overrides are user data and always win (CLAUDE.md), on the card exactly as on
        // the tile.
        var series = CreateSeriesWithCovers(
            (1, "volume/openlibrary/a.jpg", null),
            (2, "volume/openlibrary/b.jpg", "override/volume/b.jpg"));

        ToCard(series).CoverObjectKey.ShouldBe("override/volume/b.jpg");
    }

    [Fact]
    public void ToCard_AnOverrideOnAVolumeWithNoSyncedArt_IsStillTheLatestCover()
    {
        var series = CreateSeriesWithCovers(
            (1, "volume/openlibrary/a.jpg", null), (2, null, "override/volume/b.jpg"));

        ToCard(series).CoverObjectKey.ShouldBe("override/volume/b.jpg");
    }

    [Fact]
    public void ToCard_NoVolumeWithArt_FallsBackToTheSeriesCover()
    {
        // The common case on a shelf enrichment has not reached: one series cover from
        // AniList and nothing per volume.
        var series = CreateSeriesWithCovers((1, null, null), (2, null, null));

        ToCard(series).CoverObjectKey.ShouldBe("anilist/85470.jpg");
    }

    [Fact]
    public void ToCard_NoArtAnywhere_HasNoCover()
    {
        var series = CreateSeriesWithCovers((1, null, null));
        series.CoverObjectKey = null;

        // design.md's coverless placeholder is what a series with no art at all renders.
        ToCard(series).CoverObjectKey.ShouldBeNull();
    }

    [Fact]
    public void ToCard_ASeriesWithAnAuthorAndGenres_CarriesBothForTheShelfFilters()
    {
        var series = CreateSeriesWithOneVolume(null, owned: false, releaseDaysFromToday: null);
        series.Author = "Carlo Zen";
        series.Genres = ["Action", "Fantasy"];

        var card = ToCard(series);

        card.Author.ShouldBe("Carlo Zen");
        card.Genres.ShouldBe(["Action", "Fantasy"]);
    }

    private static Series CreateSeriesWithCovers(
        params (int Number, string? VolumeCover, string? Override)[] volumes)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            TitleEnglish = "Test Series",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
            CoverObjectKey = "anilist/85470.jpg",
            UserState = new SeriesUserState { TrackingStatus = TrackingStatus.Reading },
        };

        foreach (var volume in volumes)
        {
            series.Volumes.Add(new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = volume.Number,
                CoverObjectKey = volume.VolumeCover,
                UserState = volume.Override is null
                    ? null
                    : new VolumeUserState { CoverOverrideObjectKey = volume.Override },
            });
        }

        return series;
    }

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
