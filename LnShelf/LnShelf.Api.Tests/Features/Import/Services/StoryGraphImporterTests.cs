using LnShelf.Api.Data;
using LnShelf.Api.Tests.Features.Import.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Import.Services;

/// <summary>
/// Staging a batch: the real export in, one group per series out, and the spelling the
/// review queue is going to print.
/// </summary>
// The matcher is faked — this is about the grouping and staging around it, and the
// matcher has its own tests. Nothing here reaches AniList.
public sealed class StoryGraphImporterTests
{
    [Fact]
    public async Task CreateBatchAsync_TheRealExport_StagesEveryBookInElevenGroups()
    {
        await using var dbContext = CreateDbContext();
        var importer = CreateImporter(dbContext);

        await using var export = StoryGraphExportFixture.OpenRead();
        var response = await importer.CreateBatchAsync(
            StoryGraphExportFixture.FileName, export, CancellationToken.None);

        response.RowCount.ShouldBe(StoryGraphExportFixture.ExpectedBookCount);
        response.GroupCount.ShouldBe(StoryGraphExportFixture.ExpectedGroupCount);

        var batch = await LoadBatchAsync(dbContext);
        batch.Groups.Count.ShouldBe(StoryGraphExportFixture.ExpectedGroupCount);
        batch.Groups.Sum(group => group.Rows.Count).ShouldBe(StoryGraphExportFixture.ExpectedBookCount);

        // Read-implies-owned is on by default; the review queue is where it gets
        // switched off for a series that was borrowed.
        batch.Groups.ShouldAllBe(group => group.TreatReadAsOwned);
    }

    [Fact]
    public async Task CreateBatchAsync_ASeriesSpelledTwoWays_NamesItTheWayMostRowsDo()
    {
        await using var dbContext = CreateDbContext();
        var importer = CreateImporter(dbContext);

        await using var export = StoryGraphExportFixture.OpenRead();
        await importer.CreateBatchAsync(
            StoryGraphExportFixture.FileName, export, CancellationToken.None);

        // The real file writes this series ten times with an em dash and once with two
        // hyphens. Both normalise to one group, and the majority spelling is the one the
        // queue prints.
        var batch = await LoadBatchAsync(dbContext);
        var group = batch.Groups.Single(candidate => candidate.SeriesKey == "86 eighty six");

        group.SeriesTitle.ShouldBe("86—EIGHTY-SIX");
        group.Rows.Count.ShouldBe(11);
    }

    [Fact]
    public async Task CreateBatchAsync_TwoSpellingsTied_PicksTheSameOneEveryTime()
    {
        await using var dbContext = CreateDbContext();
        var importer = CreateImporter(dbContext);

        // A tie has to break somewhere, and it breaks on the title itself rather than on
        // the order the file happened to list the rows in — otherwise the queue would
        // rename the series between two uploads of the same export.
        const string csv = """
            Title,Read Status
            "Overlord!, Vol. 2",read
            "Overlord, Vol. 1",read
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        await importer.CreateBatchAsync("tie.csv", stream, CancellationToken.None);

        var batch = await LoadBatchAsync(dbContext);
        batch.Groups.Single().SeriesTitle.ShouldBe("Overlord");
    }

    [Fact]
    public async Task CreateBatchAsync_TheRealExport_CarriesSubtitlesOntoTheStagedRows()
    {
        await using var dbContext = CreateDbContext();
        var importer = CreateImporter(dbContext);

        await using var export = StoryGraphExportFixture.OpenRead();
        await importer.CreateBatchAsync(
            StoryGraphExportFixture.FileName, export, CancellationToken.None);

        var batch = await LoadBatchAsync(dbContext);
        var rows = batch.Groups.SelectMany(group => group.Rows).ToList();

        rows.Single(row => row.RawTitle == "Overlord, Vol. 3: The Bloody Valkyrie")
            .Subtitle.ShouldBe("The Bloody Valkyrie");

        // Whatever the count, it is the free half of what the subtitle backfill spends
        // an Open Library lookup per volume on.
        rows.Count(row => row.Subtitle is not null).ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CreateBatchAsync_TheRealExport_AsksTheMatcherForTheGroupsMajorityCredits()
    {
        await using var dbContext = CreateDbContext();
        var matcher = CreateUnmatchingMatcher();
        var importer = CreateImporter(dbContext, matcher: matcher);

        await using var export = StoryGraphExportFixture.OpenRead();
        await importer.CreateBatchAsync(
            StoryGraphExportFixture.FileName, export, CancellationToken.None);

        // One row of the eleven credits the translator as the illustrator, which is
        // StoryGraph's own data being wrong. The majority of the group says Shirabii,
        // and that is what the tie-break gets — a single mistyped contributor must not
        // decide which series eleven books belong to.
        await matcher.Received(1).MatchAsync(
            "86—EIGHTY-SIX", "Asato Asato", "Shirabii", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateBatchAsync_AnEmptyFile_IsRefusedRatherThanStaged()
    {
        await using var dbContext = CreateDbContext();
        var importer = CreateImporter(dbContext);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Title,Read Status\n"));

        var act = async () => await importer.CreateBatchAsync(
            "empty.csv", stream, CancellationToken.None);

        await act.ShouldThrowAsync<LnShelf.Api.Errors.InvalidInputException>();
        (await dbContext.ImportBatches.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task CreateBatchAsync_MoreRowsThanTheLimit_IsRefusedRatherThanStaged()
    {
        await using var dbContext = CreateDbContext();
        var importer = CreateImporter(dbContext, new ImportOptions { MaximumRowCount = 2 });

        await using var export = StoryGraphExportFixture.OpenRead();

        var act = async () => await importer.CreateBatchAsync(
            StoryGraphExportFixture.FileName, export, CancellationToken.None);

        await act.ShouldThrowAsync<LnShelf.Api.Errors.InvalidInputException>();
        (await dbContext.ImportBatches.CountAsync()).ShouldBe(0);
    }

    private static Task<ImportBatch> LoadBatchAsync(LnShelfDbContext dbContext) =>
        dbContext.ImportBatches
            .Include(batch => batch.Groups).ThenInclude(group => group.Rows)
            .SingleAsync();

    /// <summary>
    /// No candidates: every group goes to review, which is what keeps these tests
    /// about grouping and staging rather than about matching.
    /// </summary>
    private static ISeriesMatcher CreateUnmatchingMatcher()
    {
        var matcher = Substitute.For<ISeriesMatcher>();
        matcher.MatchAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new SeriesMatch
            {
                Candidates = [],
                Status = ImportGroupStatus.NeedsReview,
                Reason = ImportReviewReason.NoCandidate,
            });

        return matcher;
    }

    private static StoryGraphImporter CreateImporter(
        LnShelfDbContext dbContext,
        ImportOptions? options = null,
        ISeriesMatcher? matcher = null) =>
        new(new StoryGraphCsvReader(NullLogger<StoryGraphCsvReader>.Instance),
            new ImportBatchBuilder(
                dbContext,
                matcher ?? CreateUnmatchingMatcher(),
                Substitute.For<ICoverCache>(),
                Options.Create(options ?? new ImportOptions()),
                TimeProvider.System,
                NullLogger<ImportBatchBuilder>.Instance));

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"import-staging-{Guid.CreateVersion7()}")
            .Options);
}
