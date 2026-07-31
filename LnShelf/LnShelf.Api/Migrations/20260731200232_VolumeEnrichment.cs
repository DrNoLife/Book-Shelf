using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    // Hand-ordered after scaffolding. EF put the two DropColumn calls first, which would
    // have thrown away every discrepancy already recorded — the live shelf has four
    // waiting for review. The order here is create, copy, then drop, and the Down does the
    // same in reverse, so the change is reversible with its data intact.
    public partial class VolumeEnrichment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sync_discrepancy_value",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SyncDiscrepancyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Value = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_discrepancy_value", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sync_discrepancy_value_sync_discrepancy_SyncDiscrepancyId",
                        column: x => x.SyncDiscrepancyId,
                        principalTable: "sync_discrepancy",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sync_discrepancy_value_SyncDiscrepancyId",
                table: "sync_discrepancy_value",
                column: "SyncDiscrepancyId");

            // Every recorded disagreement, moved from two named columns to one row per
            // source. A null value meant "this source said nothing", which is not a
            // position worth a row of its own — the sentence reads the same without it.
            migrationBuilder.Sql("""
                INSERT INTO sync_discrepancy_value ("Id", "SyncDiscrepancyId", "Source", "Value")
                SELECT gen_random_uuid(), discrepancy."Id", sourced."Source", sourced."Value"
                FROM sync_discrepancy AS discrepancy
                CROSS JOIN LATERAL (
                    VALUES
                        ('AniList', discrepancy."AniListValue"),
                        ('MangaUpdates', discrepancy."MangaUpdatesValue")
                ) AS sourced("Source", "Value")
                WHERE sourced."Value" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "AniListValue",
                table: "sync_discrepancy");

            migrationBuilder.DropColumn(
                name: "MangaUpdatesValue",
                table: "sync_discrepancy");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "volume",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EnrichmentFailures",
                table: "sync_run",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EnrichmentGaps",
                table: "sync_run",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VolumeCoversCached",
                table: "sync_run",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "VolumesEnriched",
                table: "sync_run",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Title",
                table: "volume");

            migrationBuilder.DropColumn(
                name: "EnrichmentFailures",
                table: "sync_run");

            migrationBuilder.DropColumn(
                name: "EnrichmentGaps",
                table: "sync_run");

            migrationBuilder.DropColumn(
                name: "VolumeCoversCached",
                table: "sync_run");

            migrationBuilder.DropColumn(
                name: "VolumesEnriched",
                table: "sync_run");

            migrationBuilder.AddColumn<string>(
                name: "AniListValue",
                table: "sync_discrepancy",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MangaUpdatesValue",
                table: "sync_discrepancy",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            // Back into the two columns. A third source's value has nowhere to go in the
            // old shape, so it is dropped rather than silently mislabelled as one of the
            // two — which is the whole reason the shape changed.
            migrationBuilder.Sql("""
                UPDATE sync_discrepancy AS discrepancy SET
                    "AniListValue" = (
                        SELECT sourced."Value" FROM sync_discrepancy_value AS sourced
                        WHERE sourced."SyncDiscrepancyId" = discrepancy."Id"
                          AND sourced."Source" = 'AniList'
                        LIMIT 1),
                    "MangaUpdatesValue" = (
                        SELECT sourced."Value" FROM sync_discrepancy_value AS sourced
                        WHERE sourced."SyncDiscrepancyId" = discrepancy."Id"
                          AND sourced."Source" = 'MangaUpdates'
                        LIMIT 1);
                """);

            migrationBuilder.DropTable(
                name: "sync_discrepancy_value");
        }
    }
}
