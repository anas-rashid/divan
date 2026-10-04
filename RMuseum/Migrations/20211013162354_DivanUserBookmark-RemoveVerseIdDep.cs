using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class DivanUserBookmarkRemoveVerseIdDep : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DivanUserBookmarks_UserId_PoemId_VerseId",
                table: "DivanUserBookmarks");

            migrationBuilder.AlterColumn<int>(
                name: "VerseId",
                table: "DivanUserBookmarks",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_DivanUserBookmarks_UserId_PoemId_VerseId",
                table: "DivanUserBookmarks",
                columns: new[] { "UserId", "PoemId", "VerseId" },
                unique: true,
                filter: "[VerseId] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DivanUserBookmarks_UserId_PoemId_VerseId",
                table: "DivanUserBookmarks");

            migrationBuilder.AlterColumn<int>(
                name: "VerseId",
                table: "DivanUserBookmarks",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DivanUserBookmarks_UserId_PoemId_VerseId",
                table: "DivanUserBookmarks",
                columns: new[] { "UserId", "PoemId", "VerseId" },
                unique: true);
        }
    }
}
