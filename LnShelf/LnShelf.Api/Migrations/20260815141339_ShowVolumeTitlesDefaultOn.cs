using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    public partial class ShowVolumeTitlesDefaultOn : Migration
    {
        /// <inheritdoc />
        // The column was added with a store default of false, matching the model's default
        // at the time. The model now defaults it on (volume titles are real data since the
        // catalogue pass), so the store's opinion is brought back in line with it — the
        // same pairing LibraryView and VolumeView already have.
        //
        // Deliberately no UPDATE: a stored false is the user's answer, and a migration that
        // overwrote it would be sync overwriting user state, one table over.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "ShowVolumeTitles",
                table: "user_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "ShowVolumeTitles",
                table: "user_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);
        }
    }
}
