using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Import.Fixtures;

/// <summary>
/// The staging half of the pipeline, wired the way <c>AddImport()</c> wires it:
/// both real importers over one shared <see cref="ImportBatchBuilder"/>, and the
/// resolver that picks between them.
/// </summary>
// Built from the real classes rather than substitutes, because what these tests
// are about is that the seam actually holds two implementations — a faked
// importer would prove nothing about that.
internal static class ImportPipelineHarness
{
    public static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"import-staging-{Guid.CreateVersion7()}")
            .Options, TestUsers.Current);

    /// <summary>
    /// A matcher that finds nothing, so every group goes to review — which keeps
    /// the staging tests about grouping rather than about matching.
    /// </summary>
    public static ISeriesMatcher CreateUnmatchingMatcher()
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

    public static ImportBatchBuilder CreateBuilder(
        LnShelfDbContext dbContext,
        ISeriesMatcher? matcher = null,
        ImportOptions? options = null) =>
        new(dbContext,
            matcher ?? CreateUnmatchingMatcher(),
            Substitute.For<ISeriesCoverCache>(),
            Options.Create(options ?? new ImportOptions()),
            TimeProvider.System,
            NullLogger<ImportBatchBuilder>.Instance);

    public static StoryGraphImporter CreateStoryGraphImporter(ImportBatchBuilder builder) =>
        new(new StoryGraphCsvReader(NullLogger<StoryGraphCsvReader>.Instance), builder);

    public static GoodreadsImporter CreateGoodreadsImporter(ImportBatchBuilder builder) =>
        new(new GoodreadsCsvReader(NullLogger<GoodreadsCsvReader>.Instance), builder);

    /// <summary>Both importers behind the seam, as the composition root registers them.</summary>
    public static ImportSourceResolver CreateResolver(ImportBatchBuilder builder) =>
        new([CreateStoryGraphImporter(builder), CreateGoodreadsImporter(builder)]);

    public static Task<ImportBatch> LoadBatchAsync(LnShelfDbContext dbContext) =>
        dbContext.ImportBatches
            .Include(batch => batch.Groups).ThenInclude(group => group.Rows)
            .SingleAsync();
}
