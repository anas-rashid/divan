using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class DivanNumberings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DivanNumberings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartCatId = table.Column<int>(type: "int", nullable: false),
                    EndCatId = table.Column<int>(type: "int", nullable: true),
                    TotalLines = table.Column<int>(type: "int", nullable: false),
                    TotalVerses = table.Column<int>(type: "int", nullable: false),
                    TotalCouplets = table.Column<int>(type: "int", nullable: false),
                    TotalParagraphs = table.Column<int>(type: "int", nullable: false),
                    LastCountingDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanNumberings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanNumberings_DivanCategories_EndCatId",
                        column: x => x.EndCatId,
                        principalTable: "DivanCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DivanNumberings_DivanCategories_StartCatId",
                        column: x => x.StartCatId,
                        principalTable: "DivanCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DivanVerseNumbers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NumberingId = table.Column<int>(type: "int", nullable: false),
                    PoemId = table.Column<int>(type: "int", nullable: false),
                    CoupletIndex = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    IsPoemVerse = table.Column<bool>(type: "bit", nullable: false),
                    SameTypeNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanVerseNumbers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanVerseNumbers_DivanNumberings_NumberingId",
                        column: x => x.NumberingId,
                        principalTable: "DivanNumberings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DivanNumberings_EndCatId",
                table: "DivanNumberings",
                column: "EndCatId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanNumberings_StartCatId",
                table: "DivanNumberings",
                column: "StartCatId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanVerseNumbers_NumberingId_PoemId_CoupletIndex",
                table: "DivanVerseNumbers",
                columns: new[] { "NumberingId", "PoemId", "CoupletIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DivanVerseNumbers_PoemId_CoupletIndex",
                table: "DivanVerseNumbers",
                columns: new[] { "PoemId", "CoupletIndex" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DivanVerseNumbers");

            migrationBuilder.DropTable(
                name: "DivanNumberings");
        }
    }
}
