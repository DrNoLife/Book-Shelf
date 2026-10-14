using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Shelf.Services;

/// <summary>
/// Series kept by hand: added with no external id, grown and trimmed one local volume at a
/// time, and never allowed to reach into a series a source owns.
/// </summary>
public sealed class ManualSeriesCommandServiceTests
{
    [Fact]
    public async Task AddAsync_AWebNovelWithThreeVolumes_AddsItWithLocalVolumesAllOut()
    {
        await using var dbContext = CreateDbContext();

        var response = await CreateService(dbContext).AddAsync(
            new AddManualSeriesRequest
            {
                TitleEnglish = "  Mother of Learning  ",
                Format = Format.WebNovel,
                Author = "nobody103",
                VolumeCount = 3,
            },
            CancellationToken.None);

        response.AlreadyTracked.ShouldBeFalse();
        response.VolumeCount.ShouldBe(3);

        var series = await dbContext.Series
            .Include(candidate => candidate.Volumes)
            .Include(candidate => candidate.UserState)
            .SingleAsync();

        series.TitleEnglish.ShouldBe("Mother of Learning");
        series.AniListId.ShouldBeNull();
        series.MangaUpdatesId.ShouldBeNull();
        series.Author.ShouldBe("nobody103");
        series.VolumeCountBasis.ShouldBe(VolumeCountBasis.Manual);
        series.UserState.ShouldNotBeNull().TrackingStatus.ShouldBe(TrackingStatus.PlanToStart);
        series.Volumes.Select(volume => volume.Number).OrderBy(number => number).ShouldBe(new[] { 1m, 2m, 3m });
        series.Volumes.ShouldAllBe(volume => volume.IsLocalOnly && volume.IsReleased && volume.ExternalId == null);
    }

    [Fact]
    public async Task AddAsync_TheSameTitleAndFormatTwice_AddsOneSeries()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);
        var request = new AddManualSeriesRequest { TitleEnglish = "Mother of Learning", Format = Format.WebNovel };

        var first = await service.AddAsync(request, CancellationToken.None);
        var second = await service.AddAsync(request, CancellationToken.None);

        second.AlreadyTracked.ShouldBeTrue();
        second.SeriesId.ShouldBe(first.SeriesId);
        (await dbContext.Series.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task AddAsync_TheSameTitleInAnotherFormat_IsAnotherSeries()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        await service.AddAsync(new AddManualSeriesRequest { TitleEnglish = "Overlord", Format = Format.WebNovel }, CancellationToken.None);
        var manga = await service.AddAsync(new AddManualSeriesRequest { TitleEnglish = "Overlord", Format = Format.Manga }, CancellationToken.None);

        manga.AlreadyTracked.ShouldBeFalse();
        (await dbContext.Series.CountAsync()).ShouldBe(2);
    }

    [Theory]
    [InlineData(Format.Manga, ProgressUnit.Chapters, ProgressUnit.Chapters)]
    [InlineData(Format.WebNovel, ProgressUnit.Chapters, ProgressUnit.Chapters)]
    [InlineData(Format.LightNovel, ProgressUnit.Chapters, ProgressUnit.Volumes)]
    [InlineData(Format.Manga, ProgressUnit.Volumes, ProgressUnit.Volumes)]
    public async Task AddAsync_ProgressUnit_FollowsThePreferenceWhereTheFormatOffersChapters(
        Format format,
        ProgressUnit preferred,
        ProgressUnit expected)
    {
        await using var dbContext = CreateDbContext();
        await TestPreferences.ChooseDefaultProgressUnitAsync(dbContext, preferred);

        await CreateService(dbContext).AddAsync(
            new AddManualSeriesRequest { TitleEnglish = "Overlord", Format = format }, CancellationToken.None);

        var series = await dbContext.Series.Include(candidate => candidate.UserState).SingleAsync();
        series.UserState.ShouldNotBeNull().ProgressUnit.ShouldBe(expected);
    }

    [Theory]
    [InlineData("   ", null, 1)]
    [InlineData("A title", null, -1)]
    [InlineData("A title", null, AddManualSeriesRequest.MaximumVolumeCount + 1)]
    public async Task AddAsync_InputTheFormCannotSend_IsRefused(string title, string? author, int volumeCount)
    {
        await using var dbContext = CreateDbContext();

        await Should.ThrowAsync<InvalidInputException>(() => CreateService(dbContext).AddAsync(
            new AddManualSeriesRequest
            {
                TitleEnglish = title,
                Format = Format.WebNovel,
                Author = author,
                VolumeCount = volumeCount,
            },
            CancellationToken.None));

        (await dbContext.Series.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task AddVolumeAsync_NoNumber_TakesTheNextWholeNumber()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, 1m, 2m, 2.5m);

        var detail = await CreateService(dbContext).AddVolumeAsync(
            series.Id, new AddLocalVolumeRequest(), CancellationToken.None);

        var added = detail.ShouldNotBeNull().Volumes.Single(volume => volume.Number == 3m);
        added.IsLocalOnly.ShouldBeTrue();
        added.IsOut.ShouldBeTrue();
    }

    [Fact]
    public async Task AddVolumeAsync_ABonusNumber_IsAddedBetweenTheMainVolumes()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, 1m, 2m);

        var detail = await CreateService(dbContext).AddVolumeAsync(
            series.Id, new AddLocalVolumeRequest { Number = 1.5m }, CancellationToken.None);

        detail.ShouldNotBeNull().Volumes.Select(volume => volume.Number).ShouldBe(new[] { 1m, 1.5m, 2m });
    }

    [Fact]
    public async Task AddVolumeAsync_ANumberAlreadyThere_IsAConflict()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, 1m, 2m);

        await Should.ThrowAsync<ConflictException>(() => CreateService(dbContext).AddVolumeAsync(
            series.Id, new AddLocalVolumeRequest { Number = 2m }, CancellationToken.None));
    }

    [Fact]
    public async Task AddVolumeAsync_ALinkedSeries_IsAConflict()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, aniListId: 85470, numbers: [1m]);

        await Should.ThrowAsync<ConflictException>(() => CreateService(dbContext).AddVolumeAsync(
            series.Id, new AddLocalVolumeRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task AddVolumeAsync_NoSuchSeries_IsNull()
    {
        await using var dbContext = CreateDbContext();

        (await CreateService(dbContext).AddVolumeAsync(
            Guid.CreateVersion7(), new AddLocalVolumeRequest(), CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task RemoveVolumeAsync_ALocalVolume_RemovesItWithItsStateAndItsUploadedCover()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, 1m, 2m);
        var volume = series.Volumes.Single(candidate => candidate.Number == 2m);
        var overrideKey = CoverObjectKey.ForVolumeOverride(volume.Id, Guid.CreateVersion7(), ".webp");
        volume.UserState = new VolumeUserState { ReadStatus = ReadStatus.Read, CoverOverrideObjectKey = overrideKey };
        await dbContext.SaveChangesAsync();

        var coverStore = Substitute.For<ICoverStore>();
        var detail = await CreateService(dbContext, coverStore).RemoveVolumeAsync(
            series.Id, volume.Id, CancellationToken.None);

        detail.ShouldNotBeNull().Volumes.Select(candidate => candidate.Number).ShouldBe(new[] { 1m });
        (await dbContext.Volumes.CountAsync()).ShouldBe(1);
        (await dbContext.VolumeUserStates.CountAsync()).ShouldBe(0);
        await coverStore.Received(1).DeleteAsync(overrideKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveVolumeAsync_AVolumeASourceSupplied_IsAConflict()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, 1m);
        var volume = series.Volumes.Single();
        volume.IsLocalOnly = false;
        volume.ExternalId = "1";
        await dbContext.SaveChangesAsync();

        await Should.ThrowAsync<ConflictException>(() => CreateService(dbContext).RemoveVolumeAsync(
            series.Id, volume.Id, CancellationToken.None));
    }

    [Fact]
    public async Task RemoveVolumeAsync_ALinkedSeries_IsAConflict()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, mangaUpdatesId: 17992364412, numbers: [1m]);

        await Should.ThrowAsync<ConflictException>(() => CreateService(dbContext).RemoveVolumeAsync(
            series.Id, series.Volumes.Single().Id, CancellationToken.None));

        (await dbContext.Volumes.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task RemoveVolumeAsync_AVolumeOfAnotherSeries_IsNull()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext, 1m);
        var other = await SeedAsync(dbContext, 1m);

        (await CreateService(dbContext).RemoveVolumeAsync(
            series.Id, other.Volumes.Single().Id, CancellationToken.None)).ShouldBeNull();

        (await dbContext.Volumes.CountAsync()).ShouldBe(2);
    }

    private static Task<Series> SeedAsync(LnShelfDbContext dbContext, params decimal[] numbers) =>
        SeedAsync(dbContext, aniListId: null, mangaUpdatesId: null, numbers);

    private static async Task<Series> SeedAsync(
        LnShelfDbContext dbContext,
        int? aniListId = null,
        long? mangaUpdatesId = null,
        params decimal[] numbers)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = aniListId,
            MangaUpdatesId = mangaUpdatesId,
            TitleEnglish = "Mother of Learning",
            Format = Format.WebNovel,
        };

        foreach (var number in numbers)
        {
            series.Volumes.Add(new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = number,
                IsReleased = true,
                IsLocalOnly = true,
            });
        }

        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();
        return series;
    }

    private static ManualSeriesCommandService CreateService(LnShelfDbContext dbContext, ICoverStore? coverStore = null) =>
        new(dbContext,
            TestPreferences.For(dbContext),
            TestUsers.Limits(dbContext),
            coverStore ?? Substitute.For<ICoverStore>(),
            TimeProvider.System,
            NullLogger<ManualSeriesCommandService>.Instance);

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"manual-series-{Guid.CreateVersion7()}")
            .Options, TestUsers.Current);
}
