using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    public partial class ChapterProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Additive only. Every existing user-state row becomes a Volumes series through the
            // default, which is exactly how it behaved before; the release before this one never
            // names the new columns, and its inserts still land as Volumes rows
            // (.claude/rules/data-access.md: backward compatible for one release).
            migrationBuilder.AddColumn<decimal>(
                name: "LastChapterRead",
                table: "series_user_state",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProgressUnit",
                table: "series_user_state",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Volumes");

            migrationBuilder.AddColumn<decimal>(
                name: "LatestChapter",
                table: "series",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastChapterRead",
                table: "series_user_state");

            migrationBuilder.DropColumn(
                name: "ProgressUnit",
                table: "series_user_state");

            migrationBuilder.DropColumn(
                name: "LatestChapter",
                table: "series");
        }
    }
}
