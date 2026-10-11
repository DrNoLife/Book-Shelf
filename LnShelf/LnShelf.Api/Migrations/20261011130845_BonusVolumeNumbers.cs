using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    public partial class BonusVolumeNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Widening only: every existing integer is a valid numeric, and the release before
            // this one keeps working against the new columns (Npgsql reads a whole numeric into
            // an int, and Postgres casts its int parameters). That stays true only while no
            // fractional number exists, which is why bonus volumes are created a release later
            // (.claude/rules/data-access.md: backward compatible for one release).
            //
            // Unscaled numeric rather than numeric(5,1): a scaled column hands back 7.0 for 7,
            // and the shelf would print "Vol. 7.0" wherever a number is shown.
            migrationBuilder.AlterColumn<decimal>(
                name: "Number",
                table: "volume",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<decimal>(
                name: "VolumeNumber",
                table: "sync_discrepancy",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "VolumeNumber",
                table: "source_choice",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Number",
                table: "volume",
                type: "integer",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<int>(
                name: "VolumeNumber",
                table: "sync_discrepancy",
                type: "integer",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "VolumeNumber",
                table: "source_choice",
                type: "integer",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);
        }
    }
}
