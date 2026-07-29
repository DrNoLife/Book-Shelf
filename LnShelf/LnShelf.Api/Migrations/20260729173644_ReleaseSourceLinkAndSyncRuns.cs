using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    public partial class ReleaseSourceLinkAndSyncRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "MangaUpdatesId",
                table: "series",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "sync_run",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SeriesRefreshed = table.Column<int>(type: "integer", nullable: false),
                    SeriesLinked = table.Column<int>(type: "integer", nullable: false),
                    DatesMoved = table.Column<int>(type: "integer", nullable: false),
                    NewVolumes = table.Column<int>(type: "integer", nullable: false),
                    Failures = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_run", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sync_discrepancy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SyncRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesTitle = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Field = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AniListValue = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    MangaUpdatesValue = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    ResolvedWith = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    VolumeNumber = table.Column<int>(type: "integer", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_discrepancy", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sync_discrepancy_sync_run_SyncRunId",
                        column: x => x.SyncRunId,
                        principalTable: "sync_run",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sync_discrepancy_SeriesId",
                table: "sync_discrepancy",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_sync_discrepancy_SyncRunId",
                table: "sync_discrepancy",
                column: "SyncRunId");

            migrationBuilder.CreateIndex(
                name: "IX_sync_run_StartedAt",
                table: "sync_run",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sync_discrepancy");

            migrationBuilder.DropTable(
                name: "sync_run");

            migrationBuilder.AlterColumn<int>(
                name: "MangaUpdatesId",
                table: "series",
                type: "integer",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
