using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ChapterProgressMigration = LnShelf.Api.Migrations.ChapterProgress;

namespace LnShelf.Api.Tests.Migrations;

/// <summary>
/// The chapter-progress migration leaves every existing series exactly as it was: counted in
/// volumes, with no chapter, and safe for the release before it (data-access.md).
/// </summary>
public sealed class ChapterProgressMigrationTests
{
    private static IReadOnlyList<AddColumnOperation> AddedColumns =>
        [.. new ChapterProgressMigration().UpOperations.OfType<AddColumnOperation>()];

    [Fact]
    public void Up_TheUnitColumn_DefaultsEveryExistingRowToVolumes()
    {
        var unit = AddedColumns.Single(column => column.Name == nameof(SeriesUserState.ProgressUnit));

        unit.Table.ShouldBe("series_user_state");
        unit.IsNullable.ShouldBeFalse();
        unit.DefaultValue.ShouldBe(nameof(ProgressUnit.Volumes));
    }

    [Fact]
    public void Up_OnlyAddsColumns_AndTheChapterColumnsAreNullable()
    {
        new ChapterProgressMigration().UpOperations.ShouldAllBe(operation => operation is AddColumnOperation);

        AddedColumns.Single(column => column.Name == nameof(SeriesUserState.LastChapterRead)).IsNullable.ShouldBeTrue();
        AddedColumns.Single(column => column.Name == nameof(Series.LatestChapter)).IsNullable.ShouldBeTrue();
    }
}
