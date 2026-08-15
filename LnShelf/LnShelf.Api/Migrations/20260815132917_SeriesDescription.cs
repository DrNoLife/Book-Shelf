using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeriesDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "series",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "series");
        }
    }
}
