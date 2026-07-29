using LnShelf.Api.Data;
using LnShelf.Api.Errors;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Import.Services;

/// <summary>
/// The decisions the review queue records, and the commit that applies them.
/// </summary>
public sealed class ImportCommandServiceTests
{
    private const int NovelAniListId = 85976;
    private const int MangaAniListId = 85934;

    [Fact]
    public async Task ResolveGroupAsync_ChoosingACandidate_MarksTheGroupResolved()
    {
        await using var dbContext = CreateDbContext();
        var batch = await SeedBatchAsync(dbContext);
        var group = batch.Groups.Single();

        var response = await CreateService(dbContext).ResolveGroupAsync(
            group.Id, new ResolveImportGroupRequest { AniListId = NovelAniListId }, CancellationToken.None);

        response.ShouldNotBeNull();
        response.Status.ShouldBe(ImportGroupStatus.Resolved);
        response.MatchedAniListId.ShouldBe(NovelAniListId);
        response.Reason.ShouldBe(ImportReviewReason.None);

        // The score comes from the candidate that was chosen, not from the matcher's
        // original guess.
        response.MatchScore.ShouldBe(100);
    }

    [Fact]
    public async Task ResolveGroupAsync_ChoosingTheOtherFormat_SwitchesTheMatch()
    {
        await using var dbContext = CreateDbContext();
        var batch = await SeedBatchAsync(dbContext);
        var group = batch.Groups.Single();
        var service = CreateService(dbContext);

        await service.ResolveGroupAsync(
            group.Id, new ResolveImportGroupRequest { AniListId = NovelAniListId }, CancellationToken.None);

        var response = await service.ResolveGroupAsync(
            group.Id, new ResolveImportGroupRequest { AniListId = MangaAniListId }, CancellationToken.None);

        response.ShouldNotBeNull().MatchedAniListId.ShouldBe(MangaAniListId);
    }

    [Fact]
    public async Task ResolveGroupAsync_Skipping_LeavesTheBooksOffTheShelf()
    {
        await using var dbContext = CreateDbContext();
        var batch = await SeedBatchAsync(dbContext);
        var group = batch.Groups.Single();

        var response = await CreateService(dbContext).ResolveGroupAsync(
            group.Id, new ResolveImportGroupRequest { Skip = true }, CancellationToken.None);

        response.ShouldNotBeNull();
        response.Status.ShouldBe(ImportGroupStatus.Skipped);
        response.MatchedAniListId.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveGroupAsync_TurningOffReadImpliesOwned_ChangesTheRowsOwnership()
    {
        await using var dbContext = CreateDbContext();
        var batch = await SeedBatchAsync(dbContext);
        var group = batch.Groups.Single();

        var response = await CreateService(dbContext).ResolveGroupAsync(
            group.Id,
            new ResolveImportGroupRequest { AniListId = NovelAniListId, TreatReadAsOwned = false },
            CancellationToken.None);

        response.ShouldNotBeNull();
        response.TreatReadAsOwned.ShouldBeFalse();

        // The export said Owned? No for every row, so with the rule off nothing is
        // owned — which is the whole point of it being switchable.
        response.Rows.ShouldAllBe(row => !row.Owned);
    }

    [Fact]
    public async Task ResolveGroupAsync_UnknownGroup_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();

        var response = await CreateService(dbContext).ResolveGroupAsync(
            Guid.CreateVersion7(), new ResolveImportGroupRequest { Skip = true }, CancellationToken.None);

        response.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveGroupAsync_NeitherAMatchNorASkip_IsRefused()
    {
        await using var dbContext = CreateDbContext();
        var batch = await SeedBatchAsync(dbContext);
        var group = batch.Groups.Single();

        var act = async () => await CreateService(dbContext).ResolveGroupAsync(
            group.Id, new ResolveImportGroupRequest(), CancellationToken.None);

        await act.ShouldThrowAsync<InvalidInputException>();
    }

    [Fact]
    public async Task CommitAsync_WithGroupsStillWaiting_IsRefused()
    {
        await using var dbContext = CreateDbContext();
        var batch = await SeedBatchAsync(dbContext);

        // Refused rather than partially applied: a half-committed batch leaves
        // nothing honest to report.
        var act = async () => await CreateService(dbContext).CommitAsync(batch.Id, CancellationToken.None);

        var exception = await act.ShouldThrowAsync<ConflictException>();
        exception.Message.ShouldContain("still need a decision");
    }

    [Fact]
    public async Task CommitAsync_EverythingDecided_AppliesTheBatch()
    {
        await using var dbContext = CreateDbContext();
        var batch = await SeedBatchAsync(dbContext);
        var service = CreateService(dbContext);

        await service.ResolveGroupAsync(
            batch.Groups.Single().Id,
            new ResolveImportGroupRequest { AniListId = NovelAniListId },
            CancellationToken.None);

        var result = await service.CommitAsync(batch.Id, CancellationToken.None);

        result.ShouldNotBeNull();
        result.SeriesCreated.ShouldBe(1);
        result.GroupsCommitted.ShouldBe(1);
        result.VolumeStatesWritten.ShouldBe(3);

        (await dbContext.Series.CountAsync()).ShouldBe(1);
        (await dbContext.ImportBatches.SingleAsync()).CommittedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task CommitAsync_ASkippedGroup_WritesNothingForIt()
    {
        await using var dbContext = CreateDbContext();
        var batch = await SeedBatchAsync(dbContext);
        var service = CreateService(dbContext);

        await service.ResolveGroupAsync(
            batch.Groups.Single().Id,
            new ResolveImportGroupRequest { Skip = true },
            CancellationToken.None);

        var result = await service.CommitAsync(batch.Id, CancellationToken.None);

        result.ShouldNotBeNull();
        result.SeriesCreated.ShouldBe(0);
        result.GroupsSkipped.ShouldBe(1);
        (await dbContext.Series.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task CommitAsync_TheSameBatchTwice_IsRefused()
    {
        await using var dbContext = CreateDbContext();
        var batch = await SeedBatchAsync(dbContext);
        var service = CreateService(dbContext);

        await service.ResolveGroupAsync(
            batch.Groups.Single().Id,
            new ResolveImportGroupRequest { AniListId = NovelAniListId },
            CancellationToken.None);
        await service.CommitAsync(batch.Id, CancellationToken.None);

        var act = async () => await service.CommitAsync(batch.Id, CancellationToken.None);

        // Re-applying is done by uploading the export again, which is the additive
        // path; committing one batch twice would double nothing but confuse everything.
        await act.ShouldThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task CommitAsync_UnknownBatch_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();

        (await CreateService(dbContext).CommitAsync(Guid.CreateVersion7(), CancellationToken.None))
            .ShouldBeNull();
    }

    [Fact]
    public async Task ResolveGroupAsync_ACommittedGroup_CannotBeChanged()
    {
        await using var dbContext = CreateDbContext();
        var batch = await SeedBatchAsync(dbContext);
        var service = CreateService(dbContext);
        var groupId = batch.Groups.Single().Id;

        await service.ResolveGroupAsync(
            groupId, new ResolveImportGroupRequest { AniListId = NovelAniListId }, CancellationToken.None);
        await service.CommitAsync(batch.Id, CancellationToken.None);

        var act = async () => await service.ResolveGroupAsync(
            groupId, new ResolveImportGroupRequest { Skip = true }, CancellationToken.None);

        await act.ShouldThrowAsync<ConflictException>();
    }

    /// <summary>
    /// One batch, one group, in exactly the state the real import produces: two
    /// candidates tied at 100, waiting on a person.
    /// </summary>
    private static async Task<ImportBatch> SeedBatchAsync(LnShelfDbContext dbContext)
    {
        var group = new ImportGroup
        {
            Id = Guid.CreateVersion7(),
            SeriesKey = "overlord",
            SeriesTitle = "Overlord",
            Status = ImportGroupStatus.NeedsReview,
            Reason = ImportReviewReason.FormatAmbiguous,
            TreatReadAsOwned = true,
        };

        foreach (var volumeNumber in new[] { 1, 2, 3 })
        {
            group.Rows.Add(new ImportRow
            {
                Id = Guid.CreateVersion7(),
                LineNumber = volumeNumber + 1,
                RawTitle = $"Overlord, Vol. {volumeNumber}",
                VolumeNumber = volumeNumber,
                ReadStatus = ReadStatus.Read,
                OwnedInExport = false,
                OwnedFormat = OwnedFormat.Digital,
            });
        }

        group.Candidates.Add(CreateCandidate(NovelAniListId, Format.LightNovel, rank: 0));
        group.Candidates.Add(CreateCandidate(MangaAniListId, Format.Manga, rank: 1));

        var batch = new ImportBatch
        {
            Id = Guid.CreateVersion7(),
            FileName = "story-graph-export.csv",
            UploadedAt = new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero),
            RowCount = 3,
            Groups = [group],
        };

        dbContext.ImportBatches.Add(batch);
        await dbContext.SaveChangesAsync();

        return batch;
    }

    private static ImportCandidate CreateCandidate(int aniListId, Format format, int rank) => new()
    {
        Id = Guid.CreateVersion7(),
        AniListId = aniListId,
        TitleEnglish = "Overlord",
        Format = format,
        PublicationStatus = PublicationStatus.Ongoing,
        VolumeCount = 18,
        Score = 100,
        Rank = rank,
    };

    private static ImportCommandService CreateService(LnShelfDbContext dbContext)
    {
        var metadataSource = Substitute.For<ISeriesMetadataSource>();
        metadataSource.GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => new SeriesMetadata
            {
                ExternalId = callInfo.Arg<int>(),
                TitleEnglish = "Overlord",
                Format = Format.LightNovel,
                PublicationStatus = PublicationStatus.Ongoing,
                Volumes = [.. Enumerable.Range(1, 18).Select(number => new VolumeMetadata { Number = number })],
            });

        var coverCache = Substitute.For<ICoverCache>();

        return new ImportCommandService(
            dbContext,
            new ImportShelfWriter(dbContext, metadataSource, coverCache, NullLogger<ImportShelfWriter>.Instance),
            metadataSource,
            coverCache,
            TimeProvider.System,
            NullLogger<ImportCommandService>.Instance);
    }

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"import-command-{Guid.CreateVersion7()}")
            .Options);
}
