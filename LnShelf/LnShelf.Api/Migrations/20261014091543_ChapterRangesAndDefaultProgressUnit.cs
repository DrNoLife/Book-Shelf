using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    public partial class ChapterRangesAndDefaultProgressUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChapterRangeSource",
                table: "volume",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FirstChapter",
                table: "volume",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LastChapter",
                table: "volume",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultProgressUnit",
                table: "user_preferences",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Volumes");

            migrationBuilder.AddColumn<int>(
                name: "ChapterRangesWritten",
                table: "sync_run",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ChapterListPage",
                table: "series",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ChapterMapCheckedAt",
                table: "series",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChapterMapStatus",
                table: "series",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ChapterMapSyncRunId",
                table: "series",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MangaDexId",
                table: "series",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChapterRangeSource",
                table: "volume");

            migrationBuilder.DropColumn(
                name: "FirstChapter",
                table: "volume");

            migrationBuilder.DropColumn(
                name: "LastChapter",
                table: "volume");

            migrationBuilder.DropColumn(
                name: "DefaultProgressUnit",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "ChapterRangesWritten",
                table: "sync_run");

            migrationBuilder.DropColumn(
                name: "ChapterListPage",
                table: "series");

            migrationBuilder.DropColumn(
                name: "ChapterMapCheckedAt",
                table: "series");

            migrationBuilder.DropColumn(
                name: "ChapterMapStatus",
                table: "series");

            migrationBuilder.DropColumn(
                name: "ChapterMapSyncRunId",
                table: "series");

            migrationBuilder.DropColumn(
                name: "MangaDexId",
                table: "series");
        }
    }
}
