using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    public partial class LibraryAndVolumeViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The scaffolded default for a required string column is "", which is not a
            // member of either enum: the existing preferences row would come back
            // unparseable. The defaults are the enum's own defaults instead, so a row
            // written before this migration reads as Covers / Tiles.
            migrationBuilder.AddColumn<string>(
                name: "LibraryView",
                table: "user_preferences",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Covers");

            migrationBuilder.AddColumn<string>(
                name: "VolumeView",
                table: "user_preferences",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Tiles");

            migrationBuilder.AddColumn<string>(
                name: "VolumeViewOverride",
                table: "series_user_state",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LibraryView",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "VolumeView",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "VolumeViewOverride",
                table: "series_user_state");
        }
    }
}
