using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    public partial class SourceChoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ResolvedByChoice",
                table: "sync_discrepancy",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ReleaseSourceVolumeCount",
                table: "series",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "source_choice",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    Field = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VolumeNumber = table.Column<int>(type: "integer", nullable: true),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReplacedValue = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    ChosenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_choice", x => x.Id);
                    table.ForeignKey(
                        name: "FK_source_choice_series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_source_choice_SeriesId_Field_VolumeNumber",
                table: "source_choice",
                columns: new[] { "SeriesId", "Field", "VolumeNumber" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "source_choice");

            migrationBuilder.DropColumn(
                name: "ResolvedByChoice",
                table: "sync_discrepancy");

            migrationBuilder.DropColumn(
                name: "ReleaseSourceVolumeCount",
                table: "series");
        }
    }
}
