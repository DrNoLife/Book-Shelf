using LnShelf.Api.Tests.Data.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LnShelf.Api.Tests.Features.Shelf.Services;

/// <summary>
/// Adding a series twice at the same moment: the request that loses the race answers as if
/// the series had already been there, which by then it is.
/// </summary>
public sealed class AddSeriesPipelineTests
{
    private const int AniListId = 85470;

    private readonly InMemoryDatabaseRoot _databaseRoot = new();
    private readonly string _databaseName = $"add-series-race-{Guid.CreateVersion7()}";

    [Fact]
    public async Task RunAsync_AConcurrentAddWinsTheRace_ReturnsTheWinnersSeriesAsAlreadyTracked()
    {
        var winnerId = Guid.CreateVersion7();
        var raceLost = false;

        // The in-memory provider has no unique index, so the race is staged: at this request's
        // save, the other request's row lands first and the save fails the way Postgres fails it.
        bool LoseTheRace(DbContext context)
        {
            if (raceLost || !context.ChangeTracker.Entries<Series>().Any(entry => entry.State == EntityState.Added))
            {
                return false;
            }

            using var otherRequest = CreateDbContext();
            otherRequest.Series.Add(new Series
            {
                Id = winnerId,
                AniListId = AniListId,
                TitleEnglish = "Mushoku Tensei: Jobless Reincarnation",
                Format = Format.LightNovel,
                PublicationStatus = PublicationStatus.Completed,
            });
            otherRequest.SaveChanges();

            raceLost = true;
            return true;
        }

        await using var dbContext = CreateDbContext(
            new RefusingSaveInterceptor(LoseTheRace, RefusingSaveInterceptor.UniqueViolation));

        var response = await CreatePipeline(dbContext).RunAsync(
            new AddSeriesRequest { AniListId = AniListId, Format = Format.LightNovel },
            CancellationToken.None);

        // What the sequential path answers for a series already on the shelf — not a 500.
        response.ShouldNotBeNull().SeriesId.ShouldBe(winnerId);
        response.AlreadyTracked.ShouldBeTrue();

        // And the loser's own copy is gone from the context, so nothing re-inserts it later.
        dbContext.ChangeTracker.Entries().ShouldNotContain(entry => entry.State == EntityState.Added);

        await using var saved = CreateDbContext();
        (await saved.Series.CountAsync()).ShouldBe(1);
    }

    private static AddSeriesPipeline CreatePipeline(LnShelfDbContext dbContext)
    {
        var metadataSource = Substitute.For<ISeriesMetadataSource>();
        metadataSource.GetSeriesAsync(AniListId, Arg.Any<CancellationToken>())
            .Returns(new SeriesMetadata
            {
                ExternalId = AniListId,
                TitleEnglish = "Mushoku Tensei: Jobless Reincarnation",
                Format = Format.LightNovel,
                PublicationStatus = PublicationStatus.Completed,
                Volumes = [.. Enumerable.Range(1, 3).Select(number => new VolumeMetadata { Number = number })],
            });

        return new AddSeriesPipeline(
            dbContext,
            [new AniListAddSeriesFlow(metadataSource, Substitute.For<ICoverCache>(), TimeProvider.System)],
            Substitute.For<IIsbnBackfill>(),
            Substitute.For<ISeriesSyncService>(),
            TestUsers.Limits(dbContext),
            NullLogger<AddSeriesPipeline>.Instance);
    }

    private LnShelfDbContext CreateDbContext(RefusingSaveInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase(_databaseName, _databaseRoot);

        if (interceptor is not null)
        {
            options.AddInterceptors(interceptor);
        }

        return new LnShelfDbContext(options.Options, TestUsers.Current);
    }
}
