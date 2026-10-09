using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    public partial class SyncRunCoversWholeShelf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CoversWholeShelf",
                table: "sync_run",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // The runs that already exist, told apart by the lead their summary was written
            // with: a sweep's starts "<count> series refreshed", a one-series refresh's
            // starts with the series' title (SeriesSyncService).
            migrationBuilder.Sql("""
                UPDATE sync_run
                SET "CoversWholeShelf" = TRUE
                WHERE "Summary" ~ '^[0-9]+ series refreshed';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoversWholeShelf",
                table: "sync_run");
        }
    }
}
