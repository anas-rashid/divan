using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class DivanUserBookmarkCoupletIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DivanUserBookmarks_UserId_PoemId_VerseId",
                table: "DivanUserBookmarks");

            migrationBuilder.CreateIndex(
                name: "IX_DivanUserBookmarks_UserId_PoemId_CoupletIndex",
                table: "DivanUserBookmarks",
                columns: new[] { "UserId", "PoemId", "CoupletIndex" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DivanUserBookmarks_UserId_PoemId_CoupletIndex",
                table: "DivanUserBookmarks");

            migrationBuilder.CreateIndex(
                name: "IX_DivanUserBookmarks_UserId_PoemId_VerseId",
                table: "DivanUserBookmarks",
                columns: new[] { "UserId", "PoemId", "VerseId" },
                unique: true,
                filter: "[VerseId] IS NOT NULL");
        }
    }
}
