using LnShelf.Api.Features.Preferences.Models;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ChapterRangesMigration = LnShelf.Api.Migrations.ChapterRangesAndDefaultProgressUnit;
using DropDefaultLibraryFilterMigration = LnShelf.Api.Migrations.DropDefaultLibraryFilter;

namespace LnShelf.Api.Tests.Migrations;

/// <summary>
/// The 0.32.0 migrations stay safe for the release before them (data-access.md): the 0.31.0
/// image, which maps none of the new columns, must still insert every row it writes.
/// </summary>
public sealed class ChapterRangesMigrationTests
{
    private static IReadOnlyList<AddColumnOperation> AddedColumns =>
        [.. new ChapterRangesMigration().UpOperations.OfType<AddColumnOperation>()];

    [Fact]
    public void Up_OnlyAddsColumns()
    {
        new ChapterRangesMigration().UpOperations.ShouldAllBe(operation => operation is AddColumnOperation);
    }

    [Fact]
    public void Up_EveryRequiredColumn_HasADatabaseDefault()
    {
        AddedColumns
            .Where(column => !column.IsNullable)
            .ShouldAllBe(column => column.DefaultValue != null);
    }

    [Fact]
    public void Up_TheDefaultProgressUnit_IsVolumesForEveryExistingReader()
    {
        var unit = AddedColumns.Single(column => column.Name == nameof(UserPreferences.DefaultProgressUnit));

        unit.Table.ShouldBe("user_preferences");
        unit.DefaultValue.ShouldBe(nameof(ProgressUnit.Volumes));
    }

    [Fact]
    public void DropDefaultLibraryFilter_DropsOnlyThatColumn()
    {
        var drop = new DropDefaultLibraryFilterMigration().UpOperations.ShouldHaveSingleItem()
            .ShouldBeOfType<DropColumnOperation>();

        drop.Table.ShouldBe("user_preferences");
        drop.Name.ShouldBe("DefaultLibraryFilter");
    }
}
