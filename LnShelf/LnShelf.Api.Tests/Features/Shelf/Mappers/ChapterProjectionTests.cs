namespace LnShelf.Api.Tests.Features.Shelf.Mappers;

/// <summary>
/// The chapter fields on both projections: carried as stored, Volumes for a series nothing was
/// recorded on, and the volume-based counts unchanged by any of it.
/// </summary>
public sealed class ChapterProjectionTests
{
    private static readonly DateOnly Today = new(2026, 10, 11);

    private static readonly Func<Series, LibraryCardResponse> ToCard = LibraryCardProjection.ToCard(Today).Compile();

    [Fact]
    public void ToCard_AChaptersSeries_CarriesTheUnitAndBothChapters()
    {
        var card = ToCard(CreateSeries(new SeriesUserState
        {
            TrackingStatus = TrackingStatus.Reading,
            ProgressUnit = ProgressUnit.Chapters,
            LastChapterRead = 142m,
        }));

        card.ProgressUnit.ShouldBe(ProgressUnit.Chapters);
        card.LastChapterRead.ShouldBe(142m);
        card.LatestChapter.ShouldBe(232m);
    }

    [Fact]
    public void ToDetail_AChaptersSeries_CarriesTheUnitBothChaptersAndWhatIsLeftToRead()
    {
        var detail = SeriesDetailProjection.ToDetail(
            CreateSeries(new SeriesUserState
            {
                TrackingStatus = TrackingStatus.Reading,
                ProgressUnit = ProgressUnit.Chapters,
                LastChapterRead = 142m,
            }),
            Today);

        detail.ProgressUnit.ShouldBe(ProgressUnit.Chapters);
        detail.LastChapterRead.ShouldBe(142m);
        detail.LatestChapter.ShouldBe(232m);
        detail.ChaptersToRead.ShouldBe(90m);
    }

    // The state every series had before the choice existed, and every untouched one still has.
    [Fact]
    public void BothProjections_ASeriesWithNoStateRow_CountInVolumesWithNoChapter()
    {
        var series = CreateSeries(userState: null);

        var card = ToCard(series);
        card.ProgressUnit.ShouldBe(ProgressUnit.Volumes);
        card.LastChapterRead.ShouldBeNull();

        var detail = SeriesDetailProjection.ToDetail(series, Today);
        detail.ProgressUnit.ShouldBe(ProgressUnit.Volumes);
        detail.LastChapterRead.ShouldBeNull();
        detail.ChaptersToRead.ShouldBeNull();
    }

    // Chapters are a reading position, not volume state: To buy, Backlog and Behind read the
    // same whatever chapter is recorded (CLAUDE.md, derived states).
    [Fact]
    public void BothProjections_AChapterFarAhead_LeavesTheVolumeCountsAlone()
    {
        var withoutChapters = CreateSeries(new SeriesUserState { TrackingStatus = TrackingStatus.Reading });
        var withChapters = CreateSeries(new SeriesUserState
        {
            TrackingStatus = TrackingStatus.Reading,
            ProgressUnit = ProgressUnit.Chapters,
            LastChapterRead = 230m,
        });

        var before = ToCard(withoutChapters);
        var after = ToCard(withChapters);
        (after.Read, after.Owned, after.Published, after.ToBuy, after.Backlog, after.Behind)
            .ShouldBe((before.Read, before.Owned, before.Published, before.ToBuy, before.Backlog, before.Behind));

        SeriesDetailProjection.ToDetail(withChapters, Today).Counts
            .ShouldBe(SeriesDetailProjection.ToDetail(withoutChapters, Today).Counts);
    }

    /// <summary>Chainsaw Man: Vol 1 read, Vol 2 owned and unread, Vol 3 out and not owned.</summary>
    private static Series CreateSeries(SeriesUserState? userState)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            TitleEnglish = "Chainsaw Man",
            Format = Format.Manga,
            PublicationStatus = PublicationStatus.Ongoing,
            LatestChapter = 232m,
            UserState = userState,
        };

        (ReadStatus Read, bool Owned)[] states = [(ReadStatus.Read, true), (ReadStatus.Unread, true), (ReadStatus.Unread, false)];
        for (var index = 0; index < states.Length; index++)
        {
            var volume = new Volume { Id = Guid.CreateVersion7(), Number = index + 1, IsReleased = true };
            volume.UserState = new VolumeUserState
            {
                VolumeId = volume.Id,
                ReadStatus = states[index].Read,
                Owned = states[index].Owned,
            };
            series.Volumes.Add(volume);
        }

        return series;
    }
}
