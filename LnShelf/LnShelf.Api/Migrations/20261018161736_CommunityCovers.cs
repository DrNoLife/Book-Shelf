using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    public partial class CommunityCovers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CoverSharingSince",
                table: "user_preferences",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "community_cover",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UploaderUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceVolumeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AniListId = table.Column<int>(type: "integer", nullable: true),
                    MangaUpdatesId = table.Column<long>(type: "bigint", nullable: true),
                    Format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Number = table.Column<decimal>(type: "numeric", nullable: false),
                    ContentHash = table.Column<string>(type: "char(64)", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_cover", x => x.Id);
                    table.CheckConstraint("ck_community_cover_external_id", "\"AniListId\" IS NOT NULL OR \"MangaUpdatesId\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_community_cover_app_user_UploaderUserId",
                        column: x => x.UploaderUserId,
                        principalTable: "app_user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_community_cover_volume_SourceVolumeId",
                        column: x => x.SourceVolumeId,
                        principalTable: "volume",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_cover_hide",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentHash = table.Column<string>(type: "char(64)", nullable: false),
                    HiddenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_cover_hide", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_cover_hide_app_user_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "app_user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_community_cover_AniListId_Format_Number",
                table: "community_cover",
                columns: new[] { "AniListId", "Format", "Number" },
                filter: "\"AniListId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_community_cover_ContentHash",
                table: "community_cover",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_community_cover_MangaUpdatesId_Format_Number",
                table: "community_cover",
                columns: new[] { "MangaUpdatesId", "Format", "Number" },
                filter: "\"MangaUpdatesId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_community_cover_SourceVolumeId",
                table: "community_cover",
                column: "SourceVolumeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_community_cover_UploaderUserId",
                table: "community_cover",
                column: "UploaderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_community_cover_hide_OwnerId_ContentHash",
                table: "community_cover_hide",
                columns: new[] { "OwnerId", "ContentHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_cover");

            migrationBuilder.DropTable(
                name: "community_cover_hide");

            migrationBuilder.DropColumn(
                name: "CoverSharingSince",
                table: "user_preferences");
        }
    }
}
