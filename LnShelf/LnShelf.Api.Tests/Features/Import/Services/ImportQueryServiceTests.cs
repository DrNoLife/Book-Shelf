using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Import.Services;

/// <summary>
/// The import reads: an id that is not there, and a batch that belongs to someone else,
/// both read as "no such batch" (audit 2026-10-09, #33).
/// </summary>
public sealed class ImportQueryServiceTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetBatchAsync_UnknownBatch_ReturnsNull()
    {
        await using var dbContext = CreateDbContext(NewDatabaseName(), TestUsers.Current);

        var batch = await CreateService(dbContext).GetBatchAsync(Guid.CreateVersion7(), CancellationToken.None);

        batch.ShouldBeNull();
    }

    [Fact]
    public async Task GetQueueAsync_UnknownBatch_ReturnsNull()
    {
        await using var dbContext = CreateDbContext(NewDatabaseName(), TestUsers.Current);

        var queue = await CreateService(dbContext).GetQueueAsync(Guid.CreateVersion7(), CancellationToken.None);

        queue.ShouldBeNull();
    }

    // The batch id is in the URL, so guessing or sharing one must not show another user's import.
    [Fact]
    public async Task GetBatchQueueAndRecent_AnotherUsersBatch_AreInvisible()
    {
        var databaseName = NewDatabaseName();
        var batchId = await AddBatchAsync(databaseName, TestUsers.SomeoneElse, Noon);

        await using var dbContext = CreateDbContext(databaseName, TestUsers.Current);
        var service = CreateService(dbContext);

        (await service.GetBatchAsync(batchId, CancellationToken.None)).ShouldBeNull();
        (await service.GetQueueAsync(batchId, CancellationToken.None)).ShouldBeNull();
        (await service.GetRecentBatchesAsync(10, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task GetRecentBatchesAsync_SeveralBatches_ReturnsTheNewestUpToTheLimit()
    {
        var databaseName = NewDatabaseName();
        await AddBatchAsync(databaseName, TestUsers.Owner, Noon);
        var newest = await AddBatchAsync(databaseName, TestUsers.Owner, Noon.AddDays(2));
        var middle = await AddBatchAsync(databaseName, TestUsers.Owner, Noon.AddDays(1));

        await using var dbContext = CreateDbContext(databaseName, TestUsers.Current);

        var batches = await CreateService(dbContext).GetRecentBatchesAsync(2, CancellationToken.None);

        batches.Select(batch => batch.BatchId).ShouldBe([newest, middle]);
    }

    private static async Task<Guid> AddBatchAsync(string databaseName, Guid ownerId, DateTimeOffset uploadedAt)
    {
        await using var dbContext = CreateDbContext(databaseName, TestUsers.As(ownerId));
        var batch = new ImportBatch
        {
            Id = Guid.CreateVersion7(),
            FileName = "export.csv",
            UploadedAt = uploadedAt,
        };

        dbContext.ImportBatches.Add(batch);
        await dbContext.SaveChangesAsync();
        return batch.Id;
    }

    private static ImportQueryService CreateService(LnShelfDbContext dbContext) =>
        new(dbContext,
            Substitute.For<ISeriesMetadataSource>(),
            Substitute.For<ISeriesCoverCache>(),
            Options.Create(new ImportOptions()));

    private static string NewDatabaseName() => $"import-query-{Guid.CreateVersion7()}";

    private static LnShelfDbContext CreateDbContext(string databaseName, ICurrentUser currentUser) =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options, currentUser);
}
