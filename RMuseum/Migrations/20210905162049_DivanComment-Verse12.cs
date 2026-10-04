using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class DivanCommentVerse12 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Verse12d",
                table: "DivanComments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Verse1Id",
                table: "DivanComments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Verse2Id",
                table: "DivanComments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DivanComments_Verse1Id",
                table: "DivanComments",
                column: "Verse1Id");

            migrationBuilder.CreateIndex(
                name: "IX_DivanComments_Verse2Id",
                table: "DivanComments",
                column: "Verse2Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DivanComments_DivanVerses_Verse1Id",
                table: "DivanComments",
                column: "Verse1Id",
                principalTable: "DivanVerses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DivanComments_DivanVerses_Verse2Id",
                table: "DivanComments",
                column: "Verse2Id",
                principalTable: "DivanVerses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DivanComments_DivanVerses_Verse1Id",
                table: "DivanComments");

            migrationBuilder.DropForeignKey(
                name: "FK_DivanComments_DivanVerses_Verse2Id",
                table: "DivanComments");

            migrationBuilder.DropIndex(
                name: "IX_DivanComments_Verse1Id",
                table: "DivanComments");

            migrationBuilder.DropIndex(
                name: "IX_DivanComments_Verse2Id",
                table: "DivanComments");

            migrationBuilder.DropColumn(
                name: "Verse12d",
                table: "DivanComments");

            migrationBuilder.DropColumn(
                name: "Verse1Id",
                table: "DivanComments");

            migrationBuilder.DropColumn(
                name: "Verse2Id",
                table: "DivanComments");
        }
    }
}
