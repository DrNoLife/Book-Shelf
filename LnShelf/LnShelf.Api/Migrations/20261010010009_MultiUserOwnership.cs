using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    public partial class MultiUserOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sync_run_StartedAt",
                table: "sync_run");

            migrationBuilder.DropIndex(
                name: "IX_series_AniListId",
                table: "series");

            migrationBuilder.DropIndex(
                name: "IX_series_MangaUpdatesId",
                table: "series");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "user_preferences",
                newName: "OwnerId");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "sync_run",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "series",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "import_batch",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "app_user",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_user", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sync_run_OwnerId_StartedAt",
                table: "sync_run",
                columns: new[] { "OwnerId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_series_OwnerId_AniListId",
                table: "series",
                columns: new[] { "OwnerId", "AniListId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_series_OwnerId_MangaUpdatesId",
                table: "series",
                columns: new[] { "OwnerId", "MangaUpdatesId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_import_batch_OwnerId",
                table: "import_batch",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_app_user_Issuer_Subject",
                table: "app_user",
                columns: new[] { "Issuer", "Subject" },
                unique: true);

            // The library from before users existed belongs to a placeholder user, created only
            // when there is such a library. Auth:InitialOwnerEmail adopts this row in place on
            // its first sign-in (UserSignIn), so no row has to be re-pointed later. Issuer
            // "lnshelf" is never issued by a provider, so nobody else can sign in as it.
            migrationBuilder.Sql("""
                INSERT INTO app_user ("Id", "Issuer", "Subject", "Email", "CreatedAt", "LastSeenAt")
                SELECT '00000000-0000-0000-0000-00000000a001', 'lnshelf', 'initial-owner', '', now(), now()
                WHERE EXISTS (SELECT 1 FROM series)
                   OR EXISTS (SELECT 1 FROM import_batch)
                   OR EXISTS (SELECT 1 FROM sync_run)
                   OR EXISTS (SELECT 1 FROM user_preferences);

                UPDATE series SET "OwnerId" = '00000000-0000-0000-0000-00000000a001';
                UPDATE import_batch SET "OwnerId" = '00000000-0000-0000-0000-00000000a001';
                UPDATE sync_run SET "OwnerId" = '00000000-0000-0000-0000-00000000a001';
                UPDATE user_preferences SET "OwnerId" = '00000000-0000-0000-0000-00000000a001';
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_import_batch_app_user_OwnerId",
                table: "import_batch",
                column: "OwnerId",
                principalTable: "app_user",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_series_app_user_OwnerId",
                table: "series",
                column: "OwnerId",
                principalTable: "app_user",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_sync_run_app_user_OwnerId",
                table: "sync_run",
                column: "OwnerId",
                principalTable: "app_user",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_preferences_app_user_OwnerId",
                table: "user_preferences",
                column: "OwnerId",
                principalTable: "app_user",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_import_batch_app_user_OwnerId",
                table: "import_batch");

            migrationBuilder.DropForeignKey(
                name: "FK_series_app_user_OwnerId",
                table: "series");

            migrationBuilder.DropForeignKey(
                name: "FK_sync_run_app_user_OwnerId",
                table: "sync_run");

            migrationBuilder.DropForeignKey(
                name: "FK_user_preferences_app_user_OwnerId",
                table: "user_preferences");

            migrationBuilder.DropTable(
                name: "app_user");

            migrationBuilder.DropIndex(
                name: "IX_sync_run_OwnerId_StartedAt",
                table: "sync_run");

            migrationBuilder.DropIndex(
                name: "IX_series_OwnerId_AniListId",
                table: "series");

            migrationBuilder.DropIndex(
                name: "IX_series_OwnerId_MangaUpdatesId",
                table: "series");

            migrationBuilder.DropIndex(
                name: "IX_import_batch_OwnerId",
                table: "import_batch");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "sync_run");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "series");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "import_batch");

            migrationBuilder.RenameColumn(
                name: "OwnerId",
                table: "user_preferences",
                newName: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_sync_run_StartedAt",
                table: "sync_run",
                column: "StartedAt");

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
        }
    }
}
