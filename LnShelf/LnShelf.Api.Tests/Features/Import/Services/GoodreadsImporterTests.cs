using LnShelf.Api.Tests.Features.Import.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Import.Services;

/// <summary>
/// Staging a Goodreads export end to end: the file in, one group per series out.
/// </summary>
// The matcher is faked — this is about the second implementation reaching the
// same shared staging as the first, which is the whole claim the seam makes.
public sealed class GoodreadsImporterTests
{
    [Fact]
    public async Task CreateBatchAsync_TheExport_StagesEveryBookInSixGroups()
    {
        await using var dbContext = ImportPipelineHarness.CreateDbContext();
        var importer = ImportPipelineHarness.CreateGoodreadsImporter(
            ImportPipelineHarness.CreateBuilder(dbContext));

        await using var export = GoodreadsExportFixture.OpenRead();
        var response = await importer.CreateBatchAsync(
            GoodreadsExportFixture.FileName, export, CancellationToken.None);

        response.RowCount.ShouldBe(GoodreadsExportFixture.ExpectedBookCount);
        response.GroupCount.ShouldBe(GoodreadsExportFixture.ExpectedGroupCount);

        var batch = await ImportPipelineHarness.LoadBatchAsync(dbContext);
        batch.FileName.ShouldBe(GoodreadsExportFixture.FileName);
        batch.Groups.Sum(group => group.Rows.Count).ShouldBe(GoodreadsExportFixture.ExpectedBookCount);

        // Read-implies-owned is on by default here too; it is the builder's rule,
        // not StoryGraph's.
        batch.Groups.ShouldAllBe(group => group.TreatReadAsOwned);
    }

    [Fact]
    public async Task CreateBatchAsync_TheExport_NamesEachGroupWithoutTheEditionMarker()
    {
        await using var dbContext = ImportPipelineHarness.CreateDbContext();
        var importer = ImportPipelineHarness.CreateGoodreadsImporter(
            ImportPipelineHarness.CreateBuilder(dbContext));

        await using var export = GoodreadsExportFixture.OpenRead();
        await importer.CreateBatchAsync(
            GoodreadsExportFixture.FileName, export, CancellationToken.None);

        var batch = await ImportPipelineHarness.LoadBatchAsync(dbContext);
        var group = batch.Groups.Single(candidate => candidate.SeriesKey == "saga of tanya the evil");

        // "(light novel)" is Goodreads' word for the edition, not part of the
        // series' name — the review queue would otherwise search AniList for it.
        group.SeriesTitle.ShouldBe("The Saga of Tanya the Evil");
        group.Rows.Select(row => row.VolumeNumber).Order().ShouldBe([12, 13]);
    }

    [Fact]
    public async Task CreateBatchAsync_TheExport_CarriesIsbnsAndDatesOntoTheStagedRows()
    {
        await using var dbContext = ImportPipelineHarness.CreateDbContext();
        var importer = ImportPipelineHarness.CreateGoodreadsImporter(
            ImportPipelineHarness.CreateBuilder(dbContext));

        await using var export = GoodreadsExportFixture.OpenRead();
        await importer.CreateBatchAsync(
            GoodreadsExportFixture.FileName, export, CancellationToken.None);

        var batch = await ImportPipelineHarness.LoadBatchAsync(dbContext);
        var row = batch.Groups
            .SelectMany(group => group.Rows)
            .Single(candidate => candidate.RawTitle == "Overlord, Vol. 1");

        row.Isbn.ShouldBe("9780316272469");
        row.ReadAt.ShouldBe(new DateOnly(2021, 3, 14));
        row.Author.ShouldBe("Kugane Maruyama");
        row.OwnedInExport.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateBatchAsync_AFileWithOnlyAHeader_IsRefusedRatherThanStaged()
    {
        await using var dbContext = ImportPipelineHarness.CreateDbContext();
        var importer = ImportPipelineHarness.CreateGoodreadsImporter(
            ImportPipelineHarness.CreateBuilder(dbContext));

        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes("Book Id,Title,Exclusive Shelf,Owned Copies\n"));

        var exception = await Should.ThrowAsync<LnShelf.Api.Errors.InvalidInputException>(
            async () => await importer.CreateBatchAsync("empty.csv", stream, CancellationToken.None));

        // The refusal names the source that was actually chosen, not a hardcoded one.
        exception.Message.ShouldContain("Goodreads");
        (await dbContext.ImportBatches.CountAsync()).ShouldBe(0);
    }
}
