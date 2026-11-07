using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    public partial class SyncDiscrepancyLookupIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sync_discrepancy_SeriesId",
                table: "sync_discrepancy");

            migrationBuilder.CreateIndex(
                name: "IX_sync_discrepancy_SeriesId_Field_VolumeNumber",
                table: "sync_discrepancy",
                columns: new[] { "SeriesId", "Field", "VolumeNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sync_discrepancy_SeriesId_Field_VolumeNumber",
                table: "sync_discrepancy");

            migrationBuilder.CreateIndex(
                name: "IX_sync_discrepancy_SeriesId",
                table: "sync_discrepancy",
                column: "SeriesId");
        }
    }
}
