using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    public partial class RecommendationsAndVolumeNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The model no longer maps DefaultLibraryFilter, but the 0.30.0 image still writes it on insert.
            // Keep the column and give it a database default so 0.31 inserts succeed and 0.30 keeps working;
            // the column itself is dropped in the release after 0.31.0 (contract step).
            migrationBuilder.AlterColumn<string>(
                name: "DefaultLibraryFilter",
                table: "user_preferences",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Behind",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyWhenDated",
                table: "volume_user_state",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "recommendation_dismissal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AniListId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DismissedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommendation_dismissal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recommendation_dismissal_app_user_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "app_user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recommendation_dismissal_OwnerId_AniListId",
                table: "recommendation_dismissal",
                columns: new[] { "OwnerId", "AniListId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recommendation_dismissal");

            migrationBuilder.DropColumn(
                name: "NotifyWhenDated",
                table: "volume_user_state");

            migrationBuilder.AlterColumn<string>(
                name: "DefaultLibraryFilter",
                table: "user_preferences",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldDefaultValue: "Behind");
        }
    }
}
