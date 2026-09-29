using LnShelf.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Import.Services;

/// <summary>
/// The one-shot that collects the ISBNs earlier imports parsed and dropped. Judged
/// on the same property as every other write here: running it twice changes nothing
/// (testing.md #3).
/// </summary>
public sealed class IsbnBackfillServiceTests
{
    private const int AniListId = 85976;

    [Fact]
    public async Task RunAsync_StagedIsbns_LandOnTheVolumesTheyIdentify()
    {
        await using var dbContext = CreateDbContext();
        await SeedShelfAsync(dbContext, volumeCount: 4);
        await SeedStagingAsync(dbContext, [(1, "9780316272247"), (2, "978-0-316-36392-1"), (3, "9780316363983")]);

        var report = await CreateService(dbContext).RunAsync(CancellationToken.None);

        report.Offered.ShouldBe(3);
        report.Written.ShouldBe(3);
        report.AlreadyRecorded.ShouldBe(0);
        report.WithoutVolume.ShouldBe(0);

        var volumes = await dbContext.Volumes.OrderBy(volume => volume.Number).ToListAsync();
        volumes[0].Isbn.ShouldBe("9780316272247");

        // Normalised on the way in, by the same rule the import commit uses.
        volumes[1].Isbn.ShouldBe("9780316363921");
        volumes[2].Isbn.ShouldBe("9780316363983");

        // Volume 4 was in the shelf but not in the export. Nothing invented for it.
        volumes[3].Isbn.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_Twice_WritesNothingTheSecondTime()
    {
        await using var dbContext = CreateDbContext();
        await SeedShelfAsync(dbContext, volumeCount: 3);
        await SeedStagingAsync(dbContext, [(1, "9780316272247"), (2, "9780316363921")]);

        await CreateService(dbContext).RunAsync(CancellationToken.None);
        var second = await CreateService(dbContext).RunAsync(CancellationToken.None);

        second.Offered.ShouldBe(2);
        second.Written.ShouldBe(0);

        // The second run's whole story: everything was already there.
        second.AlreadyRecorded.ShouldBe(2);
    }

    [Fact]
    public async Task RunAsync_AVolumeWhoseIsbnWasEditedByHand_LeavesItAlone()
    {
        await using var dbContext = CreateDbContext();
        await SeedShelfAsync(dbContext, volumeCount: 2);
        await SeedStagingAsync(dbContext, [(1, "9780316272247")]);

        var volume = await dbContext.Volumes.SingleAsync(candidate => candidate.Number == 1);
        volume.Isbn = "9781975301170";
        await dbContext.SaveChangesAsync();

        var report = await CreateService(dbContext).RunAsync(CancellationToken.None);

        report.Written.ShouldBe(0);
        report.AlreadyRecorded.ShouldBe(1);

        (await dbContext.Volumes.SingleAsync(candidate => candidate.Number == 1))
            .Isbn.ShouldBe("9781975301170");
    }

    [Fact]
    public async Task RunAsync_AStagedRowForAVolumeTheShelfDoesNotTrack_IsCounted()
    {
        await using var dbContext = CreateDbContext();
        await SeedShelfAsync(dbContext, volumeCount: 2);
        await SeedStagingAsync(dbContext, [(1, "9780316272247"), (9, "9780316398879")]);

        var report = await CreateService(dbContext).RunAsync(CancellationToken.None);

        report.Written.ShouldBe(1);

        // Said out loud rather than swallowed: a staged ISBN with nowhere to go is a
        // gap worth seeing in the run's own line.
        report.WithoutVolume.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_AGroupThatWasNeverMatched_IsIgnored()
    {
        await using var dbContext = CreateDbContext();
        await SeedShelfAsync(dbContext, volumeCount: 2);
        await SeedStagingAsync(dbContext, [(1, "9780316272247")], matchedAniListId: null);

        var report = await CreateService(dbContext).RunAsync(CancellationToken.None);

        // Unmatched staging refers to no series at all, so it cannot name a volume.
        report.Offered.ShouldBe(0);
        (await dbContext.Volumes.CountAsync(volume => volume.Isbn != null)).ShouldBe(0);
    }

    [Theory]
    [InlineData(ImportGroupStatus.AutoMatched)]
    [InlineData(ImportGroupStatus.NeedsReview)]
    [InlineData(ImportGroupStatus.Resolved)]
    [InlineData(ImportGroupStatus.Skipped)]
    public async Task RunAsync_AGroupThatWasNeverCommitted_IsIgnored(ImportGroupStatus status)
    {
        await using var dbContext = CreateDbContext();
        await SeedShelfAsync(dbContext, volumeCount: 2);
        await SeedStagingAsync(dbContext, [(1, "9780316272247")], status: status);

        var report = await CreateService(dbContext).RunAsync(CancellationToken.None);

        // A match nobody has committed is a guess, and a skipped group is books the user
        // left off the shelf on purpose.
        report.Offered.ShouldBe(0);
        (await dbContext.Volumes.CountAsync(volume => volume.Isbn != null)).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_OneVolumeStagedTwice_KeepsTheEnglishEdition()
    {
        await using var dbContext = CreateDbContext();
        await SeedShelfAsync(dbContext, volumeCount: 1);

        // The real Slime shape, arriving as two groups for one series.
        await SeedStagingAsync(dbContext, [(1, "9784896374599")], seriesTitle: "転生したらスライムだった件");
        await SeedStagingAsync(dbContext, [(1, "9781975301118")]);

        await CreateService(dbContext).RunAsync(CancellationToken.None);

        (await dbContext.Volumes.SingleAsync()).Isbn.ShouldBe("9781975301118");
    }

    private static IsbnBackfillService CreateService(LnShelfDbContext dbContext) =>
        new(dbContext, NullLogger<IsbnBackfillService>.Instance);

    private static async Task SeedShelfAsync(LnShelfDbContext dbContext, int volumeCount)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = AniListId,
            TitleEnglish = "Overlord",
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
        };

        foreach (var number in Enumerable.Range(1, volumeCount))
        {
            series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = number });
        }

        dbContext.Series.Add(series);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedStagingAsync(
        LnShelfDbContext dbContext,
        (int VolumeNumber, string? Isbn)[] rows,
        int? matchedAniListId = AniListId,
        string seriesTitle = "Overlord",
        ImportGroupStatus status = ImportGroupStatus.Committed)
    {
        var batch = new ImportBatch
        {
            Id = Guid.CreateVersion7(),
            FileName = "story-graph-export.csv",
            UploadedAt = new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero),
        };

        var group = new ImportGroup
        {
            Id = Guid.CreateVersion7(),
            ImportBatchId = batch.Id,
            SeriesTitle = seriesTitle,
            SeriesKey = TitleNormalizer.Normalize(seriesTitle),
            Status = status,
            MatchedAniListId = matchedAniListId,
        };

        foreach (var (volumeNumber, isbn) in rows)
        {
            group.Rows.Add(new ImportRow
            {
                Id = Guid.CreateVersion7(),
                ImportGroupId = group.Id,
                LineNumber = volumeNumber,
                RawTitle = $"{seriesTitle}, Vol. {volumeNumber}",
                VolumeNumber = volumeNumber,
                ReadStatus = ReadStatus.Read,
                OwnedInExport = true,
                Isbn = isbn,
            });
        }

        batch.Groups.Add(group);
        dbContext.ImportBatches.Add(batch);
        await dbContext.SaveChangesAsync();
    }

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"isbn-backfill-{Guid.CreateVersion7()}")
            .Options);
}
