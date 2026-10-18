using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    public partial class DropDefaultLibraryFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Contract step for DefaultLibraryFilters (0.31.0): the model stopped mapping the
            // single-filter column a release ago, and no image left to roll back to writes it.
            migrationBuilder.DropColumn(
                name: "DefaultLibraryFilter",
                table: "user_preferences");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultLibraryFilter",
                table: "user_preferences",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Behind");
        }
    }
}
