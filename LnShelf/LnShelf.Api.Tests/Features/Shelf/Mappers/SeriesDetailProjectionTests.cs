namespace LnShelf.Api.Tests.Features.Shelf.Mappers;

/// <summary>
/// The detail projection. Its counts have to agree with LibraryCardProjection's, and
/// the spend block has to disappear entirely rather than render zeroes.
/// </summary>
public sealed class SeriesDetailProjectionTests
{
    private static readonly DateOnly Today = new(2026, 7, 29);

    [Fact]
    public void ToDetail_ASeriesWithNoPricesAnywhere_HasNoSpendBlock()
    {
        // The real shape of an imported library: owned volumes, no prices.
        var series = CreateSeries(
            (1, null, true, ReadStatus.Read, null),
            (2, null, true, ReadStatus.Read, null));

        SeriesDetailProjection.ToDetail(series, Today).Spend.ShouldBeNull();
    }

    [Fact]
    public void ToDetail_SomeVolumesPriced_AveragesOverThePricedOnesOnly()
    {
        // Averaging over volumes owned rather than volumes priced would report 90 kr
        // here, understating what was actually recorded.
        var series = CreateSeries(
            (1, null, true, ReadStatus.Read, 180m),
            (2, null, true, ReadStatus.Read, 90m),
            (3, null, true, ReadStatus.Read, null));

        var spend = SeriesDetailProjection.ToDetail(series, Today).Spend.ShouldNotBeNull();

        spend.PricedCount.ShouldBe(2);
        spend.Total.ShouldBe(270m);
        spend.Average.ShouldBe(135m);
    }

    [Theory]
    // released, owned, isReleased -> isOut
    [InlineData(-1, false, false, true)]   // dated in the past
    [InlineData(30, false, false, false)]  // dated in the future
    [InlineData(null, false, true, true)]  // the release source says it is in print
    [InlineData(null, true, false, true)]  // owned, so it must be out
    [InlineData(null, false, false, false)] // nothing known

    // Both at once, which is what per-volume enrichment made possible and what nothing
    // exercised before it. A date in the past agrees with the flag; a date in the future
    // contradicts it, and the flag still decides, because the formula is disjunctive by
    // design. The contradiction is the sync's to record, not this projection's to resolve.
    [InlineData(-1, false, true, true)]    // dated in the past and in print
    [InlineData(-1, true, true, true)]     // …and owned
    [InlineData(30, false, true, true)]    // dated in the future but in print
    [InlineData(30, true, true, true)]     // …and owned
    public void ToVolume_IsOut_UsesTheSameThreeGroundsAsTheLibraryCard(
        int? releaseDaysFromToday, bool owned, bool isReleased, bool expectedIsOut)
    {
        var volume = new Volume
        {
            Id = Guid.CreateVersion7(),
            Number = 1,
            ReleaseDate = releaseDaysFromToday is { } days ? Today.AddDays(days) : null,
            IsReleased = isReleased,
            UserState = owned ? new VolumeUserState { Owned = true } : null,
        };

        SeriesDetailProjection.ToVolume(volume, Today).IsOut.ShouldBe(expectedIsOut);
    }

    [Fact]
    public void ToDetail_Counts_MatchTheLibraryCardForTheSameSeries()
    {
        // The two projections are separate code, so this is the test that stops them
        // drifting: a tile and the card above it must never disagree about what is out.
        // Volumes 4 and 5 are the enrichment cases: a past date and a future date, both on
        // volumes the release source has marked in print.
        var series = CreateSeries(
            (1, null, true, ReadStatus.Read, null),
            (2, null, true, ReadStatus.Unread, null),
            (3, null, false, ReadStatus.Unread, null),
            (4, -30, false, ReadStatus.Unread, null),
            (5, 120, false, ReadStatus.Unread, null));

        foreach (var number in (int[])[3, 4, 5])
        {
            series.Volumes.Single(volume => volume.Number == number).IsReleased = true;
        }

        var detail = SeriesDetailProjection.ToDetail(series, Today);
        var card = LibraryCardProjection.ToCard(Today).Compile()(series);

        detail.Counts.Read.ShouldBe(card.Read);
        detail.Counts.Owned.ShouldBe(card.Owned);
        detail.Counts.Published.ShouldBe(card.Published);
        detail.Counts.ToBuy.ShouldBe(card.ToBuy);
        detail.Counts.Backlog.ShouldBe(card.Backlog);
        detail.Counts.Tracked.ShouldBe(card.Tracked);
        detail.Counts.NextToBuyNumber.ShouldBe(card.NextToBuyNumber);
    }

    [Fact]
    public void ToDetail_NotOutVolumes_AreSplitIntoDatedAndUndated()
    {
        var series = CreateSeries(
            (1, null, true, ReadStatus.Read, null),
            (2, 30, false, ReadStatus.Unread, null),
            (3, null, false, ReadStatus.Unread, null));

        var counts = SeriesDetailProjection.ToDetail(series, Today).Counts;

        counts.NotOut.ShouldBe(2);

        // One carries a date and can print it; the other can only say "no date".
        counts.Dated.ShouldBe(1);
    }

    [Fact]
    public void ToDetail_VolumesOutOfOrder_AreReturnedByNumber()
    {
        var series = CreateSeries(
            (3, null, true, ReadStatus.Read, null),
            (1, null, true, ReadStatus.Read, null),
            (2, null, true, ReadStatus.Read, null));

        SeriesDetailProjection.ToDetail(series, Today)
            .Volumes.Select(volume => volume.Number).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void ToDetail_ASeriesWithNoUserStateRow_ReadsAsPlanToStartWithNoNotes()
    {
        var series = CreateSeries((1, null, false, ReadStatus.Unread, null));
        series.UserState = null;

        var detail = SeriesDetailProjection.ToDetail(series, Today);

        detail.TrackingStatus.ShouldBe(TrackingStatus.PlanToStart);
        detail.Notes.ShouldBeNull();
        detail.StartedAt.ShouldBeNull();
    }

    [Fact]
    public void ToDetail_BlankNotes_AreNull()
    {
        // An empty note must not render an empty panel.
        var series = CreateSeries((1, null, true, ReadStatus.Read, null));
        series.UserState!.Notes = "   ";

        SeriesDetailProjection.ToDetail(series, Today).Notes.ShouldBeNull();
    }

    private static Series CreateSeries(
        params (int Number, int? ReleaseDaysFromToday, bool Owned, ReadStatus Read, decimal? Price)[] volumes)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            TitleEnglish = "The Saga of Tanya the Evil",
            TitleRomaji = "Youjo Senki",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
            Genres = ["Military", "Fantasy"],
            UserState = new SeriesUserState { TrackingStatus = TrackingStatus.Reading },
        };

        foreach (var volume in volumes)
        {
            series.Volumes.Add(new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = volume.Number,
                ReleaseDate = volume.ReleaseDaysFromToday is { } days ? Today.AddDays(days) : null,
                UserState = volume.Owned || volume.Read is not ReadStatus.Unread || volume.Price is not null
                    ? new VolumeUserState
                    {
                        ReadStatus = volume.Read,
                        Owned = volume.Owned,
                        PurchasePrice = volume.Price,
                    }
                    : null,
            });
        }

        return series;
    }
}
