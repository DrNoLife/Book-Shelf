using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    public partial class LibraryPreferencesAndAvatars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Additive only, so the release before this one keeps working against it: it never
            // reads the new columns, and a preferences row it inserts gets these defaults from
            // the database. The defaults are hand-written (the scaffold writes "") so existing
            // rows open on what the shelf showed before — Behind, and the old section order —
            // the same values UserPreferences defaults to. The single DefaultLibraryFilter
            // column stays until the release after this one (data-access.md).
            migrationBuilder.AddColumn<string>(
                name: "DefaultLibraryFilters",
                table: "user_preferences",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "Behind");

            migrationBuilder.AddColumn<string>(
                name: "LibrarySectionOrder",
                table: "user_preferences",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "ContinueReading,OutNow,Shelf");

            migrationBuilder.AddColumn<string>(
                name: "AvatarOverrideObjectKey",
                table: "app_user",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderAvatarObjectKey",
                table: "app_user",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderPictureUrl",
                table: "app_user",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultLibraryFilters",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "LibrarySectionOrder",
                table: "user_preferences");

            migrationBuilder.DropColumn(
                name: "AvatarOverrideObjectKey",
                table: "app_user");

            migrationBuilder.DropColumn(
                name: "ProviderAvatarObjectKey",
                table: "app_user");

            migrationBuilder.DropColumn(
                name: "ProviderPictureUrl",
                table: "app_user");
        }
    }
}
