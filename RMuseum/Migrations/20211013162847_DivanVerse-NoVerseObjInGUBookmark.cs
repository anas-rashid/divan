using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class DivanVerseNoVerseObjInGUBookmark : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DivanUserBookmarks_DivanVerses_Verse2Id",
                table: "DivanUserBookmarks");

            migrationBuilder.DropForeignKey(
                name: "FK_DivanUserBookmarks_DivanVerses_VerseId",
                table: "DivanUserBookmarks");

            migrationBuilder.DropIndex(
                name: "IX_DivanUserBookmarks_Verse2Id",
                table: "DivanUserBookmarks");

            migrationBuilder.DropIndex(
                name: "IX_DivanUserBookmarks_VerseId",
                table: "DivanUserBookmarks");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DivanUserBookmarks_Verse2Id",
                table: "DivanUserBookmarks",
                column: "Verse2Id");

            migrationBuilder.CreateIndex(
                name: "IX_DivanUserBookmarks_VerseId",
                table: "DivanUserBookmarks",
                column: "VerseId");

            migrationBuilder.AddForeignKey(
                name: "FK_DivanUserBookmarks_DivanVerses_Verse2Id",
                table: "DivanUserBookmarks",
                column: "Verse2Id",
                principalTable: "DivanVerses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DivanUserBookmarks_DivanVerses_VerseId",
                table: "DivanUserBookmarks",
                column: "VerseId",
                principalTable: "DivanVerses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
