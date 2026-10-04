using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class DivanUserBookmarks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DivanUserBookmarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PoemId = table.Column<int>(type: "int", nullable: false),
                    CoupletIndex = table.Column<int>(type: "int", nullable: false),
                    VerseId = table.Column<int>(type: "int", nullable: false),
                    Verse2Id = table.Column<int>(type: "int", nullable: true),
                    DateTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanUserBookmarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanUserBookmarks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DivanUserBookmarks_DivanPoems_PoemId",
                        column: x => x.PoemId,
                        principalTable: "DivanPoems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DivanUserBookmarks_DivanVerses_Verse2Id",
                        column: x => x.Verse2Id,
                        principalTable: "DivanVerses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DivanUserBookmarks_DivanVerses_VerseId",
                        column: x => x.VerseId,
                        principalTable: "DivanVerses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DivanUserBookmarks_PoemId",
                table: "DivanUserBookmarks",
                column: "PoemId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanUserBookmarks_UserId_PoemId_VerseId",
                table: "DivanUserBookmarks",
                columns: new[] { "UserId", "PoemId", "VerseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DivanUserBookmarks_Verse2Id",
                table: "DivanUserBookmarks",
                column: "Verse2Id");

            migrationBuilder.CreateIndex(
                name: "IX_DivanUserBookmarks_VerseId",
                table: "DivanUserBookmarks",
                column: "VerseId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DivanUserBookmarks");
        }
    }
}
