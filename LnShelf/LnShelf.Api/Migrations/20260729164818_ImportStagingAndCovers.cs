using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LnShelf.Api.Migrations
{
    /// <inheritdoc />
    public partial class ImportStagingAndCovers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverObjectKey",
                table: "series",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "import_batch",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    CommittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_batch", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "import_group",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesKey = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    SeriesTitle = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MatchedAniListId = table.Column<int>(type: "integer", nullable: true),
                    MatchScore = table.Column<int>(type: "integer", nullable: true),
                    TreatReadAsOwned = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_group", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_group_import_batch_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "import_batch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "import_candidate",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    AniListId = table.Column<int>(type: "integer", nullable: false),
                    TitleEnglish = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    TitleRomaji = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    TitleNative = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PublicationStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VolumeCount = table.Column<int>(type: "integer", nullable: true),
                    StartYear = table.Column<int>(type: "integer", nullable: true),
                    CoverObjectKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_candidate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_candidate_import_group_ImportGroupId",
                        column: x => x.ImportGroupId,
                        principalTable: "import_group",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "import_row",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    RawTitle = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Author = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Isbn = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    VolumeNumber = table.Column<int>(type: "integer", nullable: true),
                    ReadStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OwnedInExport = table.Column<bool>(type: "boolean", nullable: false),
                    OwnedFormat = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ReadAt = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_row", x => x.Id);
                    table.ForeignKey(
                        name: "FK_import_row_import_group_ImportGroupId",
                        column: x => x.ImportGroupId,
                        principalTable: "import_group",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_import_candidate_ImportGroupId_Rank",
                table: "import_candidate",
                columns: new[] { "ImportGroupId", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_import_group_ImportBatchId_SeriesKey",
                table: "import_group",
                columns: new[] { "ImportBatchId", "SeriesKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_import_row_ImportGroupId",
                table: "import_row",
                column: "ImportGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "import_candidate");

            migrationBuilder.DropTable(
                name: "import_row");

            migrationBuilder.DropTable(
                name: "import_group");

            migrationBuilder.DropTable(
                name: "import_batch");

            migrationBuilder.DropColumn(
                name: "CoverObjectKey",
                table: "series");
        }
    }
}
