using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialShelf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "series",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AniListId = table.Column<int>(type: "integer", nullable: true),
                    MangaUpdatesId = table.Column<int>(type: "integer", nullable: true),
                    Format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TitleEnglish = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    TitleRomaji = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    TitleNative = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    PublicationStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Publisher = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartYear = table.Column<int>(type: "integer", nullable: true),
                    Genres = table.Column<string[]>(type: "text[]", nullable: false),
                    Author = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Illustrator = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RelatedSeriesId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_series", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "series_user_state",
                columns: table => new
                {
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackingStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    StartedAt = table.Column<DateOnly>(type: "date", nullable: true),
                    LastReadAt = table.Column<DateOnly>(type: "date", nullable: true),
                    TitleLanguageOverride = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CoverSourceOverride = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    NotificationsMuted = table.Column<bool>(type: "boolean", nullable: false),
                    HasNewVolume = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_series_user_state", x => x.SeriesId);
                    table.ForeignKey(
                        name: "FK_series_user_state_series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "volume",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    ReleaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CoverObjectKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsLocalOnly = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_volume", x => x.Id);
                    table.ForeignKey(
                        name: "FK_volume_series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "volume_user_state",
                columns: table => new
                {
                    VolumeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReadStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReadAt = table.Column<DateOnly>(type: "date", nullable: true),
                    Owned = table.Column<bool>(type: "boolean", nullable: false),
                    OwnedFormat = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PurchasedAt = table.Column<DateOnly>(type: "date", nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    CoverOverrideObjectKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_volume_user_state", x => x.VolumeId);
                    table.ForeignKey(
                        name: "FK_volume_user_state_volume_VolumeId",
                        column: x => x.VolumeId,
                        principalTable: "volume",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_series_AniListId",
                table: "series",
                column: "AniListId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_series_MangaUpdatesId",
                table: "series",
                column: "MangaUpdatesId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_volume_SeriesId_Number",
                table: "volume",
                columns: new[] { "SeriesId", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "series_user_state");

            migrationBuilder.DropTable(
                name: "volume_user_state");

            migrationBuilder.DropTable(
                name: "volume");

            migrationBuilder.DropTable(
                name: "series");
        }
    }
}
