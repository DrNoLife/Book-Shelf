using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Shelf.Services;

/// <summary>
/// The header totals and the sidebar shelf counts. Both are counted across the whole
/// format scope, not across the filtered subset — the sidebar has to say how many series
/// are on a shelf you are not currently looking at.
/// </summary>
// LibraryCardProjectionTests covers the per-series rules; this covers the aggregation
// over them, and the scope rule that separates Cards from Totals and Shelves.
public sealed class LibraryQueryServiceTests
{
    [Fact]
    public async Task GetLibraryAsync_AnEmptyShelf_IsZeroesRatherThanNothing()
    {
        await using var dbContext = CreateDbContext();

        var response = await CreateService(dbContext)
            .GetLibraryAsync(format: null, [LibraryFilter.All], CancellationToken.None);

        response.Cards.ShouldBeEmpty();
        response.Totals.SeriesCount.ShouldBe(0);
        response.Totals.ToBuy.ShouldBe(0);
        response.Shelves.Reading.ShouldBe(0);
        response.Shelves.BacklogVolumes.ShouldBe(0);
    }

    [Fact]
    public async Task GetLibraryAsync_AShelfOfSeries_SumsTheVolumeTotals()
    {
        await using var dbContext = CreateDbContext();

        // 3 volumes: read+owned, owned+unread (backlog), out+unowned (to buy).
        Add(dbContext, "Tanya", Format.LightNovel, TrackingStatus.Reading,
            (ReadStatus.Read, true), (ReadStatus.Unread, true), (ReadStatus.Unread, false));

        // 2 volumes: both out and unowned.
        Add(dbContext, "Overlord", Format.LightNovel, TrackingStatus.PlanToStart,
            (ReadStatus.Unread, false), (ReadStatus.Unread, false));

        await dbContext.SaveChangesAsync();

        var totals = (await CreateService(dbContext)
            .GetLibraryAsync(format: null, [LibraryFilter.All], CancellationToken.None)).Totals;

        totals.SeriesCount.ShouldBe(2);
        totals.Read.ShouldBe(1);
        totals.Owned.ShouldBe(2);

        // Every volume here is IsReleased, and ownership is a ground of its own.
        totals.Published.ShouldBe(5);
        totals.Backlog.ShouldBe(1);
        totals.ToBuy.ShouldBe(3);
    }

    /// <summary>
    /// Shelf counts are series counts — except backlog, which counts volumes. The sidebar
    /// labels it "43 vols" for exactly that reason.
    /// </summary>
    [Fact]
    public async Task GetLibraryAsync_TheSidebarShelves_CountSeriesExceptBacklogWhichCountsVolumes()
    {
        await using var dbContext = CreateDbContext();

        // Behind: one to buy. Backlog: two owned and unread.
        Add(dbContext, "Tanya", Format.LightNovel, TrackingStatus.Reading,
            (ReadStatus.Unread, true), (ReadStatus.Unread, true), (ReadStatus.Unread, false));

        // Caught up: everything owned and read, so not behind.
        Add(dbContext, "Overlord", Format.LightNovel, TrackingStatus.Completed,
            (ReadStatus.Read, true), (ReadStatus.Read, true));

        Add(dbContext, "Slime", Format.Manga, TrackingStatus.Dropped, (ReadStatus.Read, true));

        await dbContext.SaveChangesAsync();

        var shelves = (await CreateService(dbContext)
            .GetLibraryAsync(format: null, [LibraryFilter.All], CancellationToken.None)).Shelves;

        shelves.Reading.ShouldBe(1);
        shelves.Completed.ShouldBe(1);
        shelves.Dropped.ShouldBe(1);
        shelves.PlanToStart.ShouldBe(0);

        // One series is behind; the other two are not.
        shelves.BehindOnBuying.ShouldBe(1);
        shelves.UpToDate.ShouldBe(2);

        // Volumes, not series: Tanya's two owned-and-unread ones.
        shelves.BacklogVolumes.ShouldBe(2);
    }

    /// <summary>
    /// Totals and shelf counts follow the *format* scope but ignore the filter — the
    /// sidebar has to say how many series are on a shelf you are not looking at.
    /// </summary>
    [Fact]
    public async Task GetLibraryAsync_AFilter_NarrowsTheCardsButNotTheCountsAroundThem()
    {
        await using var dbContext = CreateDbContext();

        Add(dbContext, "Tanya", Format.LightNovel, TrackingStatus.Reading, (ReadStatus.Read, true));
        Add(dbContext, "Overlord", Format.LightNovel, TrackingStatus.Dropped, (ReadStatus.Read, true));
        await dbContext.SaveChangesAsync();

        var response = await CreateService(dbContext)
            .GetLibraryAsync(format: null, [LibraryFilter.Reading], CancellationToken.None);

        response.Cards.Select(card => card.TitleEnglish).ShouldBe(["Tanya"]);
        response.Totals.SeriesCount.ShouldBe(2);
        response.Shelves.Dropped.ShouldBe(1);
    }

    /// <summary>
    /// Several status filters show their union; All, or no filter at all, shows everything;
    /// one filter alone behaves as the single value always did.
    /// </summary>
    [Theory]
    [MemberData(nameof(FilterSets))]
    public async Task GetLibraryAsync_SeveralFilters_ShowTheUnionOfThem(LibraryFilter[] filters, string[] expected)
    {
        await using var dbContext = CreateDbContext();

        // Owned, read up to date, reading.
        Add(dbContext, "Tanya", Format.LightNovel, TrackingStatus.Reading, (ReadStatus.Read, true));

        // Nothing owned, one to buy: behind, plan to start.
        Add(dbContext, "Overlord", Format.LightNovel, TrackingStatus.PlanToStart, (ReadStatus.Unread, false));

        // Owned and dropped, up to date.
        Add(dbContext, "Slime", Format.Manga, TrackingStatus.Dropped, (ReadStatus.Read, true));

        await dbContext.SaveChangesAsync();

        var cards = (await CreateService(dbContext)
            .GetLibraryAsync(format: null, filters, CancellationToken.None)).Cards;

        cards.Select(card => card.TitleEnglish).Order().ShouldBe(expected);
    }

    public static TheoryData<LibraryFilter[], string[]> FilterSets => new()
    {
        { [LibraryFilter.Owned], ["Slime", "Tanya"] },
        { [LibraryFilter.Reading], ["Tanya"] },
        { [LibraryFilter.Behind, LibraryFilter.Reading], ["Overlord", "Tanya"] },
        { [LibraryFilter.Reading, LibraryFilter.Dropped, LibraryFilter.Reading], ["Slime", "Tanya"] },
        { [LibraryFilter.All], ["Overlord", "Slime", "Tanya"] },
        { [LibraryFilter.All, LibraryFilter.Reading], ["Overlord", "Slime", "Tanya"] },
        { [], ["Overlord", "Slime", "Tanya"] },
    };

    [Fact]
    public async Task GetLibraryAsync_AFormat_ScopesTheCountsTooNotJustTheCards()
    {
        await using var dbContext = CreateDbContext();

        Add(dbContext, "Tanya", Format.LightNovel, TrackingStatus.Reading, (ReadStatus.Read, true));
        Add(dbContext, "Slime", Format.Manga, TrackingStatus.Reading, (ReadStatus.Read, true));
        await dbContext.SaveChangesAsync();

        var response = await CreateService(dbContext)
            .GetLibraryAsync(Format.Manga, [LibraryFilter.All], CancellationToken.None);

        response.Cards.Select(card => card.TitleEnglish).ShouldBe(["Slime"]);

        // Format is the scope; everything is counted inside it.
        response.Totals.SeriesCount.ShouldBe(1);
        response.Shelves.Reading.ShouldBe(1);
    }

    [Fact]
    public async Task GetLibraryAsync_TheCards_AreMostBehindFirstThenAlphabetical()
    {
        await using var dbContext = CreateDbContext();

        // Behind by 2 (both to buy).
        Add(dbContext, "Zebra", Format.LightNovel, TrackingStatus.Reading,
            (ReadStatus.Unread, false), (ReadStatus.Unread, false));

        // Behind by 1, and alphabetically first of the two ties below it.
        Add(dbContext, "Apple", Format.LightNovel, TrackingStatus.Reading, (ReadStatus.Unread, false));
        Add(dbContext, "Banana", Format.LightNovel, TrackingStatus.Reading, (ReadStatus.Unread, false));

        await dbContext.SaveChangesAsync();

        var cards = (await CreateService(dbContext)
            .GetLibraryAsync(format: null, [LibraryFilter.All], CancellationToken.None)).Cards;

        cards.Select(card => card.TitleEnglish).ShouldBe(["Zebra", "Apple", "Banana"]);
    }

    private static void Add(
        LnShelfDbContext dbContext,
        string title,
        Format format,
        TrackingStatus trackingStatus,
        params (ReadStatus Read, bool Owned)[] volumes)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            TitleEnglish = title,
            Format = format,
            PublicationStatus = PublicationStatus.Ongoing,
            UserState = new SeriesUserState { TrackingStatus = trackingStatus },
        };

        var number = 1;
        foreach (var volume in volumes)
        {
            series.Volumes.Add(new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = number++,

                // In print with no date — the real shape of this shelf.
                IsReleased = true,
                UserState = new VolumeUserState { ReadStatus = volume.Read, Owned = volume.Owned },
            });
        }

        dbContext.Series.Add(series);
    }

    private static LibraryQueryService CreateService(LnShelfDbContext dbContext) =>
        new(dbContext, TimeProvider.System);

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"library-{Guid.CreateVersion7()}")
            .Options, TestUsers.Current);
}
