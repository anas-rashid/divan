using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class DivanRemoveTajik : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TajikPages");

            migrationBuilder.DropTable(
                name: "TajikPoems");

            migrationBuilder.DropTable(
                name: "TajikVerses");

            migrationBuilder.DropTable(
                name: "TajikCats");

            migrationBuilder.DropTable(
                name: "TajikPoets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TajikPages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    TajikHtmlText = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TajikPages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TajikPoets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    BirthYearInLHijri = table.Column<int>(type: "int", nullable: false),
                    TajikDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TajikNickname = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TajikPoets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TajikVerses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    PoemId = table.Column<int>(type: "int", nullable: false),
                    TajikText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TajikVerses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TajikCats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    PoetId = table.Column<int>(type: "int", nullable: false),
                    TajikDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TajikTitle = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TajikCats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TajikCats_TajikPoets_PoetId",
                        column: x => x.PoetId,
                        principalTable: "TajikPoets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TajikPoems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CatId = table.Column<int>(type: "int", nullable: false),
                    FullTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FullUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TajikPlainText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TajikTitle = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TajikPoems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TajikPoems_TajikCats_CatId",
                        column: x => x.CatId,
                        principalTable: "TajikCats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TajikCats_PoetId",
                table: "TajikCats",
                column: "PoetId");

            migrationBuilder.CreateIndex(
                name: "IX_TajikPoems_CatId",
                table: "TajikPoems",
                column: "CatId");
        }
    }
}
