namespace LnShelf.Api.Tests.Features.Shelf.Services;

/// <summary>
/// The status chips and sidebar shelves both run through this, so every branch
/// gets a case.
/// </summary>
public sealed class LibraryCardFiltersTests
{
    /// <summary>
    /// toBuy, backlog and tracking status against each filter. "Behind" is
    /// either pile being non-empty; "Up to date" is both being empty.
    /// </summary>
    public static TheoryData<int, int, TrackingStatus, LibraryFilter, bool> Cases => new()
    {
        //  toBuy  backlog  tracking                     filter                       matches
        {   0,     0,       TrackingStatus.Reading,      LibraryFilter.All,           true  },
        {   3,     3,       TrackingStatus.Reading,      LibraryFilter.All,           true  },

        // Owned = backlog in CreateCard: any volume owned puts the series on the owned shelf.
        {   0,     1,       TrackingStatus.Dropped,      LibraryFilter.Owned,         true  },
        {   3,     0,       TrackingStatus.Reading,      LibraryFilter.Owned,         false },

        {   3,     0,       TrackingStatus.Reading,      LibraryFilter.Behind,        true  },
        {   0,     3,       TrackingStatus.Reading,      LibraryFilter.Behind,        true  },
        {   3,     3,       TrackingStatus.Reading,      LibraryFilter.Behind,        true  },
        {   0,     0,       TrackingStatus.Reading,      LibraryFilter.Behind,        false },

        {   0,     1,       TrackingStatus.Reading,      LibraryFilter.Backlog,       true  },
        {   5,     0,       TrackingStatus.Reading,      LibraryFilter.Backlog,       false },

        {   0,     0,       TrackingStatus.Reading,      LibraryFilter.UpToDate,      true  },
        {   1,     0,       TrackingStatus.Reading,      LibraryFilter.UpToDate,      false },
        {   0,     1,       TrackingStatus.Reading,      LibraryFilter.UpToDate,      false },

        {   0,     0,       TrackingStatus.Reading,      LibraryFilter.Reading,       true  },
        {   0,     0,       TrackingStatus.Completed,    LibraryFilter.Reading,       false },
        {   0,     0,       TrackingStatus.Completed,    LibraryFilter.Completed,     true  },
        {   0,     0,       TrackingStatus.PlanToStart,  LibraryFilter.PlanToStart,   true  },
        {   0,     0,       TrackingStatus.Dropped,      LibraryFilter.Dropped,       true  },
        {   0,     0,       TrackingStatus.Reading,      LibraryFilter.Dropped,       false },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Matches_ForEachFilter_SelectsTheRightCards(
        int toBuy,
        int backlog,
        TrackingStatus trackingStatus,
        LibraryFilter filter,
        bool expected)
    {
        var card = CreateCard(toBuy, backlog, trackingStatus);

        card.Matches(filter).ShouldBe(expected);
    }

    [Fact]
    public void Matches_UnknownFilter_Throws()
    {
        var card = CreateCard(0, 0, TrackingStatus.Reading);

        Action act = () => _ = card.Matches((LibraryFilter)999);

        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    private static LibraryCardResponse CreateCard(int toBuy, int backlog, TrackingStatus trackingStatus) => new()
    {
        SeriesId = Guid.CreateVersion7(),
        TitleEnglish = "Test Series",
        Format = Format.LightNovel,
        PublicationStatus = PublicationStatus.Ongoing,
        TrackingStatus = trackingStatus,
        HasNewVolume = false,
        Read = 0,
        Owned = backlog,
        Published = toBuy,
        ToBuy = toBuy,
        Backlog = backlog,
        Tracked = toBuy + backlog,
    };
}
