using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    public partial class Round3NotificationsCalendarActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActivitySeenAt",
                table: "user_preferences",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VolumeCountBasis",
                table: "series",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VolumeCountEvidence",
                table: "series",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "calendar_feed",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_feed", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_calendar_feed_app_user_UserId",
                        column: x => x.UserId,
                        principalTable: "app_user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "push_subscription",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    P256dh = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Auth = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DeviceLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSuccessAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_push_subscription", x => x.Id);
                    table.ForeignKey(
                        name: "FK_push_subscription_app_user_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "app_user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shelf_event",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VolumeNumber = table.Column<decimal>(type: "numeric", nullable: false),
                    PreviousDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NewDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NotifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shelf_event", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shelf_event_series_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_calendar_feed_Token",
                table: "calendar_feed",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_push_subscription_OwnerId_Endpoint",
                table: "push_subscription",
                columns: new[] { "OwnerId", "Endpoint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shelf_event_NotifiedAt",
                table: "shelf_event",
                column: "NotifiedAt",
                filter: "\"NotifiedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_shelf_event_SeriesId_OccurredAt",
                table: "shelf_event",
                columns: new[] { "SeriesId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calendar_feed");

            migrationBuilder.DropTable(
                name: "push_subscription");

            migrationBuilder.DropTable(
                name: "shelf_event");

            migrationBuilder.DropColumn(
                name: "ActivitySeenAt",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "VolumeCountBasis",
                table: "series");

            migrationBuilder.DropColumn(
                name: "VolumeCountEvidence",
                table: "series");
        }
    }
}
