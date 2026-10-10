using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    public partial class ShelfShare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shelf_share",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shelf_share", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_shelf_share_app_user_UserId",
                        column: x => x.UserId,
                        principalTable: "app_user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shelf_share_Token",
                table: "shelf_share",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shelf_share");
        }
    }
}
