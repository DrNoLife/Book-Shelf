namespace LnShelf.Api.Tests.Features.Shelf.Services;

/// <summary>
/// The rule that moves a series' tracking status forward as volumes get read. Full matrix
/// coverage, because a wrong answer here silently rewrites a status the reader set.
/// </summary>
public sealed class TrackingStatusRulesTests
{
    private static readonly DateOnly Today = new(2026, 7, 29);
    private static readonly DateOnly Past = Today.AddDays(-30);
    private static readonly DateOnly Future = Today.AddDays(30);

    /// <summary>One volume's facts: what the rule can see of it.</summary>
    // Public because xUnit hands it to the theory method, which is itself public.
    public sealed record VolumeCase(
        decimal Number,
        ReadStatus Read,
        bool IsReleased = true,
        bool Owned = false,
        DateOnly? ReleaseDate = null);

    public static TheoryData<string, TrackingStatus, PublicationStatus, VolumeCase[], TrackingStatus> Matrix => new()
    {
        // Starting from PlanToStart.
        { "nothing read", TrackingStatus.PlanToStart, PublicationStatus.Ongoing,
            [new(1, ReadStatus.Unread), new(2, ReadStatus.Unread)], TrackingStatus.PlanToStart },
        { "one read, ongoing", TrackingStatus.PlanToStart, PublicationStatus.Ongoing,
            [new(1, ReadStatus.Read), new(2, ReadStatus.Unread)], TrackingStatus.Reading },
        { "one being read counts as started", TrackingStatus.PlanToStart, PublicationStatus.Ongoing,
            [new(1, ReadStatus.Reading), new(2, ReadStatus.Unread)], TrackingStatus.Reading },
        { "everything read, ongoing: caught up", TrackingStatus.PlanToStart, PublicationStatus.Ongoing,
            [new(1, ReadStatus.Read), new(2, ReadStatus.Read)], TrackingStatus.Reading },
        { "everything read, completed", TrackingStatus.PlanToStart, PublicationStatus.Completed,
            [new(1, ReadStatus.Read), new(2, ReadStatus.Read)], TrackingStatus.Completed },
        { "everything read, hiatus", TrackingStatus.PlanToStart, PublicationStatus.Hiatus,
            [new(1, ReadStatus.Read), new(2, ReadStatus.Read)], TrackingStatus.Reading },
        { "everything read, cancelled", TrackingStatus.PlanToStart, PublicationStatus.Cancelled,
            [new(1, ReadStatus.Read), new(2, ReadStatus.Read)], TrackingStatus.Reading },
        { "finished but one out volume unread", TrackingStatus.PlanToStart, PublicationStatus.Completed,
            [new(1, ReadStatus.Read), new(2, ReadStatus.Unread)], TrackingStatus.Reading },
        { "finished, one out volume only being read", TrackingStatus.PlanToStart, PublicationStatus.Completed,
            [new(1, ReadStatus.Read), new(2, ReadStatus.Reading)], TrackingStatus.Reading },

        // Starting from Reading.
        { "reading, still unread volumes", TrackingStatus.Reading, PublicationStatus.Completed,
            [new(1, ReadStatus.Read), new(2, ReadStatus.Unread)], TrackingStatus.Reading },
        { "reading, finished and fully read", TrackingStatus.Reading, PublicationStatus.Completed,
            [new(1, ReadStatus.Read), new(2, ReadStatus.Read)], TrackingStatus.Completed },
        { "reading, ongoing and fully read", TrackingStatus.Reading, PublicationStatus.Ongoing,
            [new(1, ReadStatus.Read), new(2, ReadStatus.Read)], TrackingStatus.Reading },
        { "reading with nothing read stays reading", TrackingStatus.Reading, PublicationStatus.Ongoing,
            [new(1, ReadStatus.Unread)], TrackingStatus.Reading },

        // Announced volumes are not out, so they do not block Completed.
        { "announced volume without a date does not block", TrackingStatus.Reading, PublicationStatus.Completed,
            [new(1, ReadStatus.Read), new(2, ReadStatus.Unread, IsReleased: false)], TrackingStatus.Completed },
        { "announced volume with a future date does not block", TrackingStatus.Reading, PublicationStatus.Completed,
            [new(1, ReadStatus.Read), new(2, ReadStatus.Unread, IsReleased: false, ReleaseDate: Future)],
            TrackingStatus.Completed },
        { "past-dated volume is out and blocks", TrackingStatus.Reading, PublicationStatus.Completed,
            [new(1, ReadStatus.Read), new(2, ReadStatus.Unread, IsReleased: false, ReleaseDate: Past)],
            TrackingStatus.Reading },
        { "owned announced volume is out and blocks", TrackingStatus.Reading, PublicationStatus.Completed,
            [new(1, ReadStatus.Read), new(2, ReadStatus.Unread, IsReleased: false, Owned: true)],
            TrackingStatus.Reading },

        // Bonus volumes count like any other.
        { "unread bonus volume blocks", TrackingStatus.Reading, PublicationStatus.Completed,
            [new(7, ReadStatus.Read), new(7.5m, ReadStatus.Unread), new(8, ReadStatus.Read)],
            TrackingStatus.Reading },
        { "read bonus volume completes", TrackingStatus.Reading, PublicationStatus.Completed,
            [new(7, ReadStatus.Read), new(7.5m, ReadStatus.Read), new(8, ReadStatus.Read)],
            TrackingStatus.Completed },

        // Nothing out: nothing to have finished.
        { "nothing out, completed publication", TrackingStatus.PlanToStart, PublicationStatus.Completed,
            [new(1, ReadStatus.Unread, IsReleased: false)], TrackingStatus.PlanToStart },
        { "no volumes at all", TrackingStatus.Reading, PublicationStatus.Completed,
            [], TrackingStatus.Reading },

        // Decisions of the reader's own are never overruled.
        { "dropped stays dropped", TrackingStatus.Dropped, PublicationStatus.Completed,
            [new(1, ReadStatus.Read), new(2, ReadStatus.Read)], TrackingStatus.Dropped },
        { "completed stays completed with an unread volume", TrackingStatus.Completed, PublicationStatus.Completed,
            [new(1, ReadStatus.Read), new(2, ReadStatus.Unread)], TrackingStatus.Completed },
        { "completed stays completed with nothing read", TrackingStatus.Completed, PublicationStatus.Ongoing,
            [new(1, ReadStatus.Unread)], TrackingStatus.Completed },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public void NextStatus_TheStartingStatusAndWhatIsRead_MovesOnlyForward(
        string scenario,
        TrackingStatus current,
        PublicationStatus publicationStatus,
        VolumeCase[] volumes,
        TrackingStatus expected)
    {
        var series = CreateSeries(current, publicationStatus, volumes);

        TrackingStatusRules.NextStatus(series, Today).ShouldBe(expected, scenario);
    }

    [Fact]
    public void NextStatus_ASeriesWithNoUserStateRow_ReadsAsPlanToStart()
    {
        var series = CreateSeries(TrackingStatus.PlanToStart, PublicationStatus.Ongoing, [new(1, ReadStatus.Unread)]);
        series.UserState = null;

        TrackingStatusRules.NextStatus(series, Today).ShouldBe(TrackingStatus.PlanToStart);
    }

    public static TheoryData<DateOnly?, bool, bool> IsOutGrounds
    {
        get
        {
            var data = new TheoryData<DateOnly?, bool, bool>();
            foreach (DateOnly? date in new DateOnly?[] { null, Past, Today, Future })
            {
                foreach (var isReleased in new[] { false, true })
                {
                    foreach (var owned in new[] { false, true })
                    {
                        data.Add(date, isReleased, owned);
                    }
                }
            }

            return data;
        }
    }

    /// <summary>
    /// The rule keeps its own copy of "out" because the projections' copies live inside EF
    /// expressions; this pins it to SeriesDetailProjection.ToVolume so they cannot drift.
    /// </summary>
    [Theory]
    [MemberData(nameof(IsOutGrounds))]
    public void IsOut_EveryCombinationOfTheThreeGrounds_AgreesWithTheDetailProjection(
        DateOnly? releaseDate,
        bool isReleased,
        bool owned)
    {
        var volume = new Volume
        {
            Id = Guid.CreateVersion7(),
            Number = 1,
            ReleaseDate = releaseDate,
            IsReleased = isReleased,
            UserState = new VolumeUserState { Owned = owned },
        };

        TrackingStatusRules.IsOut(volume, Today)
            .ShouldBe(SeriesDetailProjection.ToVolume(volume, Today).IsOut);
    }

    private static Series CreateSeries(
        TrackingStatus current,
        PublicationStatus publicationStatus,
        VolumeCase[] volumes)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            TitleEnglish = "Test series",
            Format = Format.LightNovel,
            PublicationStatus = publicationStatus,
            UserState = new SeriesUserState { TrackingStatus = current },
        };

        foreach (var volumeCase in volumes)
        {
            series.Volumes.Add(new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = volumeCase.Number,
                IsReleased = volumeCase.IsReleased,
                ReleaseDate = volumeCase.ReleaseDate,
                UserState = new VolumeUserState { ReadStatus = volumeCase.Read, Owned = volumeCase.Owned },
            });
        }

        return series;
    }
}
