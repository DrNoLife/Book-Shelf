using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Import.Services;

/// <summary>
/// The upload guards that have to fire before the first AniList search: how many series one
/// file may hold, and text longer than the staging columns.
/// </summary>
public sealed class ImportBatchBuilderTests
{
    [Fact]
    public async Task BuildAsync_MoreSeriesThanTheLimit_IsRefusedBeforeAnySearch()
    {
        await using var dbContext = CreateDbContext();
        var matcher = CreateUnmatchingMatcher();
        var books = Enumerable.Range(1, 4).Select(number => Book($"Series {number}")).ToList();

        var act = () => CreateBuilder(dbContext, matcher, new ImportOptions { MaximumGroupCount = 3 })
            .BuildAsync("StoryGraph", "export.csv", books, CancellationToken.None);

        // The 400 names both numbers, so the person knows how far over they are.
        var refusal = await act.ShouldThrowAsync<InvalidInputException>();
        refusal.Message.ShouldContain("4 series");
        refusal.Message.ShouldContain("up to 3");

        // Refused up front: every series past this point would have cost a search.
        await matcher.DidNotReceive().MatchAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        (await dbContext.ImportBatches.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task BuildAsync_ManyBooksInFewSeries_IsCountedBySeriesNotRows()
    {
        await using var dbContext = CreateDbContext();

        // Thirteen volumes of one series are one search, so they are one against the limit.
        var books = Enumerable.Range(1, 13).Select(_ => Book("That Time I Got Reincarnated as a Slime")).ToList();

        var response = await CreateBuilder(dbContext, CreateUnmatchingMatcher(), new ImportOptions { MaximumGroupCount = 1 })
            .BuildAsync("StoryGraph", "export.csv", books, CancellationToken.None);

        response.GroupCount.ShouldBe(1);
    }

    [Fact]
    public async Task BuildAsync_TextLongerThanItsColumns_IsCutToFitRatherThanFailingTheSave()
    {
        await using var dbContext = CreateDbContext();
        var longTitle = new string('a', 1000);
        var book = Book(longTitle) with
        {
            RawTitle = longTitle,
            Author = new string('b', 1000),
            Subtitle = new string('c', 1000),
            Isbn = new string('9', 40),
        };

        await CreateBuilder(dbContext, CreateUnmatchingMatcher(), new ImportOptions())
            .BuildAsync("StoryGraph", new string('f', 1000) + ".csv", [book], CancellationToken.None);

        var batch = await dbContext.ImportBatches
            .Include(candidate => candidate.Groups).ThenInclude(group => group.Rows)
            .SingleAsync();
        var group = batch.Groups.Single();
        var row = group.Rows.Single();

        batch.FileName.Length.ShouldBe(ImportColumnLimits.FileName);
        group.SeriesKey.Length.ShouldBe(ImportColumnLimits.SeriesKey);
        group.SeriesTitle.Length.ShouldBe(ImportColumnLimits.SeriesTitle);
        row.RawTitle.Length.ShouldBe(ImportColumnLimits.RawTitle);
        row.Author.ShouldNotBeNull().Length.ShouldBe(ImportColumnLimits.Author);
        row.Subtitle.ShouldNotBeNull().Length.ShouldBe(ImportColumnLimits.Subtitle);

        // Dropped, not cut: half an ISBN would send enrichment after the wrong book.
        row.Isbn.ShouldBeNull();
    }

    private static ImportedBook Book(string seriesTitle) => new()
    {
        LineNumber = 2,
        RawTitle = $"{seriesTitle}, Vol. 1",
        SeriesTitle = seriesTitle,
        SeriesKey = seriesTitle.ToLowerInvariant(),
        VolumeNumber = 1,
        ReadStatus = ReadStatus.Read,
        OwnedInExport = true,
    };

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

    private static ImportBatchBuilder CreateBuilder(
        LnShelfDbContext dbContext,
        ISeriesMatcher matcher,
        ImportOptions options) =>
        new(dbContext,
            matcher,
            Substitute.For<ISeriesCoverCache>(),
            Options.Create(options),
            TimeProvider.System,
            NullLogger<ImportBatchBuilder>.Instance);

    private static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"import-builder-{Guid.CreateVersion7()}")
            .Options, TestUsers.Current);
}
