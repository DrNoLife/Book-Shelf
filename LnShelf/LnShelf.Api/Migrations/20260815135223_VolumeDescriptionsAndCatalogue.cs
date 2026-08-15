using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    public partial class VolumeDescriptionsAndCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "volume",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DescriptionsWritten",
                table: "sync_run",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "volume");

            migrationBuilder.DropColumn(
                name: "DescriptionsWritten",
                table: "sync_run");
        }
    }
}
