using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    public partial class DropUnusedUserStateOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverSourceOverride",
                table: "series_user_state");

            migrationBuilder.DropColumn(
                name: "TitleLanguageOverride",
                table: "series_user_state");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverSourceOverride",
                table: "series_user_state",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleLanguageOverride",
                table: "series_user_state",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }
    }
}
