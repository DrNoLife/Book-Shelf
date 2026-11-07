using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using SyncDiscrepancyLookupIndexMigration = LnShelf.Api.Migrations.SyncDiscrepancyLookupIndex;

namespace LnShelf.Api.Tests.Migrations;

/// <summary>
/// The disagreement lookup is indexed on its whole key, and the migration that does it is safe
/// for the release before it (data-access.md): an index swap, no column or table touched.
/// </summary>
// The history of sync runs is kept, and a standing disagreement is recorded again every night,
// so a series' sync_discrepancy rows grow for as long as the app runs. NewestDisagreementAsync
// asks for one (series, field, volume) on every series-page load; on SeriesId alone that read
// every disagreement the series ever had.
public sealed class SyncDiscrepancyLookupIndexMigrationTests
{
    private static readonly string[] LookupKey = ["SeriesId", "Field", "VolumeNumber"];

    [Fact]
    public void Model_TheDisagreementLookup_IsIndexedOnItsWholeKey()
    {
        using var dbContext = new LnShelfDbContext(
            new DbContextOptionsBuilder<LnShelfDbContext>().UseInMemoryDatabase($"index-{Guid.CreateVersion7()}").Options,
            TestUsers.Current);

        var indexes = dbContext.Model.FindEntityType(typeof(SyncDiscrepancy)).ShouldNotBeNull().GetIndexes();

        indexes.Select(index => index.Properties.Select(property => property.Name).ToArray())
            .ShouldContain(columns => columns.SequenceEqual(LookupKey));
    }

    [Fact]
    public void Up_OnlySwapsTheSeriesIndexForTheLookupIndex()
    {
        var operations = new SyncDiscrepancyLookupIndexMigration().UpOperations;

        operations.Count.ShouldBe(2);
        var dropped = operations[0].ShouldBeOfType<DropIndexOperation>();
        dropped.Name.ShouldBe("IX_sync_discrepancy_SeriesId");

        var created = operations[1].ShouldBeOfType<CreateIndexOperation>();
        created.Table.ShouldBe("sync_discrepancy");
        created.Columns.ShouldBe(LookupKey);
        created.IsUnique.ShouldBeFalse();
    }
}
