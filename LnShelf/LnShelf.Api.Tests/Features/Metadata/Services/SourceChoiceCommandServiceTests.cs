using Microsoft.EntityFrameworkCore;
using static LnShelf.Api.Tests.Features.Metadata.Services.SourceChoiceTestContext;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// Choosing and resetting a source through the service: who may, what is refused, and that the
/// log of what the sources said is never rewritten.
/// </summary>
public sealed class SourceChoiceCommandServiceTests
{
    private static readonly DateTimeOffset Night = new(2026, 10, 9, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ChooseAsync_ASeriesWithARecordedDisagreement_SavesTheChoiceAndAnswersWithTheSeries()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext);

        var detail = await CreateService(dbContext).ChooseAsync(
            series.Id, DateRequest(MetadataSourceName.AppleBooks), CancellationToken.None);

        var choice = detail.ShouldNotBeNull().SourceChoices.ShouldHaveSingleItem();
        choice.Field.ShouldBe(SyncDiscrepancyField.ReleaseDate);
        choice.VolumeNumber.ShouldBe(8);
        choice.Source.ShouldBe(MetadataSourceName.AppleBooks);

        // The effect is already on the page: volume 8 is dated 2099 and so not out.
        var volume = detail.Volumes.Single(candidate => candidate.Number is 8);
        volume.ReleaseDate.ShouldBe(FirstDate);
        volume.IsOut.ShouldBeFalse();
    }

    // The owner filter on Series: another user's id answers as if it did not exist, and
    // nothing of theirs is touched.
    [Fact]
    public async Task ChooseAsync_AnotherUsersSeries_ReturnsNull()
    {
        var databaseName = $"source-choice-{Guid.CreateVersion7()}";
        Series theirs;
        await using (var otherUser = CreateDbContext(databaseName, TestUsers.As(TestUsers.SomeoneElse)))
        {
            theirs = await SeedAsync(otherUser);
        }

        await using var dbContext = CreateDbContext(databaseName, TestUsers.Current);

        var detail = await CreateService(dbContext).ChooseAsync(
            theirs.Id, DateRequest(MetadataSourceName.AppleBooks), CancellationToken.None);

        detail.ShouldBeNull();

        await using var check = CreateDbContext(databaseName, TestUsers.As(TestUsers.SomeoneElse));
        (await check.SourceChoices.CountAsync()).ShouldBe(0);
        (await check.Volumes.SingleAsync(volume => volume.SeriesId == theirs.Id && volume.Number == 8))
            .ReleaseDate.ShouldBeNull();
    }

    [Fact]
    public async Task ChooseAsync_AnUnknownSeries_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();

        var detail = await CreateService(dbContext).ChooseAsync(
            Guid.CreateVersion7(), DateRequest(MetadataSourceName.AppleBooks), CancellationToken.None);

        detail.ShouldBeNull();
    }

    [Fact]
    public async Task ChooseAsync_SeriesLink_Throws()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext);

        var request = new ChooseSourceRequest
        {
            Field = SyncDiscrepancyField.SeriesLink,
            Source = MetadataSourceName.MangaUpdates,
        };

        await Should.ThrowAsync<InvalidInputException>(() =>
            CreateService(dbContext).ChooseAsync(series.Id, request, CancellationToken.None));
    }

    [Fact]
    public async Task ChooseAsync_SourceWithNoRecordedValue_Throws()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext);

        // AniList never said anything about volume 8's date.
        await Should.ThrowAsync<InvalidInputException>(() =>
            CreateService(dbContext).ChooseAsync(
                series.Id, DateRequest(MetadataSourceName.AniList), CancellationToken.None));
    }

    [Fact]
    public async Task ChooseAsync_NothingRecordedForTheField_Throws()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext);

        var request = new ChooseSourceRequest
        {
            Field = SyncDiscrepancyField.VolumeCount,
            Source = MetadataSourceName.AniList,
        };

        await Should.ThrowAsync<InvalidInputException>(() =>
            CreateService(dbContext).ChooseAsync(series.Id, request, CancellationToken.None));
    }

    [Theory]
    [InlineData(SyncDiscrepancyField.ReleaseDate, null)]
    [InlineData(SyncDiscrepancyField.Status, 8)]
    [InlineData(SyncDiscrepancyField.VolumeCount, 8)]
    public async Task ChooseAsync_VolumeNumberAgainstTheFieldsShape_Throws(
        SyncDiscrepancyField field, int? volumeNumber)
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext);

        var request = new ChooseSourceRequest
        {
            Field = field,
            VolumeNumber = volumeNumber,
            Source = MetadataSourceName.MangaUpdates,
        };

        await Should.ThrowAsync<InvalidInputException>(() =>
            CreateService(dbContext).ChooseAsync(series.Id, request, CancellationToken.None));
    }

    [Fact]
    public async Task ChooseAsync_AVolumeTheSeriesDoesNotHave_Throws()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext);

        var request = DateRequest(MetadataSourceName.AppleBooks, volumeNumber: 99);

        await Should.ThrowAsync<InvalidInputException>(() =>
            CreateService(dbContext).ChooseAsync(series.Id, request, CancellationToken.None));
    }

    [Fact]
    public async Task ChooseAsync_ChangingTheChoice_RevertsTheOldOneFirst()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext);
        var service = CreateService(dbContext);
        await service.ChooseAsync(series.Id, DateRequest(MetadataSourceName.AppleBooks), CancellationToken.None);

        var detail = await service.ChooseAsync(
            series.Id, DateRequest(MetadataSourceName.MangaUpdates), CancellationToken.None);

        // One row, now MangaUpdates; the store's date and its effect are gone, and the count
        // calls the volume out again.
        detail.ShouldNotBeNull().SourceChoices.ShouldHaveSingleItem().Source.ShouldBe(MetadataSourceName.MangaUpdates);

        var volume = detail.Volumes.Single(candidate => candidate.Number is 8);
        volume.ReleaseDate.ShouldBeNull();
        volume.IsOut.ShouldBeTrue();
        (await dbContext.SourceChoices.CountAsync()).ShouldBe(1);
    }

    // One run recorded the store's and the library's date for volume 8 as two rows. Either
    // source the page offers can be chosen, and its own date is the one applied.
    [Theory]
    [InlineData(MetadataSourceName.AppleBooks)]
    [InlineData(MetadataSourceName.OpenLibrary)]
    public async Task ChooseAsync_TwoDateRowsInOneRun_EitherSourceAppliesItsOwnDate(MetadataSourceName source)
    {
        await using var dbContext = CreateDbContext();
        var series = await SaveSeriesAsync(dbContext);
        await SaveRunAsync(
            dbContext, Night, discrepancies: [DateDisagreement(series), LibraryDateDisagreement(series)]);

        var detail = await CreateService(dbContext).ChooseAsync(
            series.Id, DateRequest(source), CancellationToken.None);

        detail.ShouldNotBeNull().SourceChoices.ShouldHaveSingleItem().Source.ShouldBe(source);
        var volume = detail.Volumes.Single(candidate => candidate.Number is 8);
        volume.ReleaseDate.ShouldBe(source is MetadataSourceName.AppleBooks ? FirstDate : LibraryDate);
        volume.IsOut.ShouldBeFalse();
    }

    [Fact]
    public async Task ResetAsync_NoChoice_IsASuccess()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext);

        var detail = await CreateService(dbContext).ResetAsync(
            series.Id, SyncDiscrepancyField.ReleaseDate, 8, CancellationToken.None);

        detail.ShouldNotBeNull().SourceChoices.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResetAsync_AChoice_RemovesItAndRestoresTheVolume()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext);
        var service = CreateService(dbContext);
        await service.ChooseAsync(series.Id, DateRequest(MetadataSourceName.AppleBooks), CancellationToken.None);

        var detail = await service.ResetAsync(series.Id, SyncDiscrepancyField.ReleaseDate, 8, CancellationToken.None);

        detail.ShouldNotBeNull().SourceChoices.ShouldBeEmpty();
        var volume = detail.Volumes.Single(candidate => candidate.Number is 8);
        volume.ReleaseDate.ShouldBeNull();
        volume.IsOut.ShouldBeTrue();
    }

    [Fact]
    public async Task ResetAsync_KeepsEveryDiscrepancyRow()
    {
        await using var dbContext = CreateDbContext();
        var series = await SeedAsync(dbContext);
        var service = CreateService(dbContext);
        var before = await DiscrepancySnapshotAsync(dbContext);

        await service.ChooseAsync(series.Id, DateRequest(MetadataSourceName.AppleBooks), CancellationToken.None);
        var afterChoosing = await DiscrepancySnapshotAsync(dbContext);

        await service.ResetAsync(series.Id, SyncDiscrepancyField.ReleaseDate, 8, CancellationToken.None);
        var afterReset = await DiscrepancySnapshotAsync(dbContext);

        // The log records what the sources said, not what the user did about it.
        afterChoosing.ShouldBe(before);
        afterReset.ShouldBe(before);
    }

    private static async Task<Series> SeedAsync(LnShelfDbContext dbContext)
    {
        var series = await SaveSeriesAsync(dbContext);
        await SaveRunAsync(dbContext, Night, discrepancies: [DateDisagreement(series), StatusDisagreement(series)]);

        return series;
    }

    private static ChooseSourceRequest DateRequest(MetadataSourceName source, int volumeNumber = 8) => new()
    {
        Field = SyncDiscrepancyField.ReleaseDate,
        VolumeNumber = volumeNumber,
        Source = source,
    };

    private static async Task<List<string>> DiscrepancySnapshotAsync(LnShelfDbContext dbContext)
    {
        var runs = await dbContext.SyncRuns.AsNoTracking()
            .Include(run => run.Discrepancies).ThenInclude(discrepancy => discrepancy.Values)
            .ToListAsync();

        return
        [
            .. runs.SelectMany(run => run.Discrepancies)
                .Select(discrepancy => $"{discrepancy.Field}:{discrepancy.VolumeNumber}:{discrepancy.ResolvedWith}:"
                                       + $"{discrepancy.ResolvedByChoice}:"
                                       + string.Join(",", discrepancy.Values
                                           .OrderBy(value => value.Source)
                                           .Select(value => $"{value.Source}={value.Value}")))
                .Order(),
        ];
    }

    private static SourceChoiceCommandService CreateService(LnShelfDbContext dbContext) =>
        new(
            dbContext,
            new SourceChoiceApplier(TimeProvider.System),
            new SeriesDetailQueryService(dbContext, TimeProvider.System),
            TimeProvider.System);
}
