using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class DivanVerseCoupletIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CoupletIndex",
                table: "DivanVerses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DivanVerses_PoemId_CoupletIndex",
                table: "DivanVerses",
                columns: new[] { "PoemId", "CoupletIndex" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DivanVerses_PoemId_CoupletIndex",
                table: "DivanVerses");

            migrationBuilder.DropColumn(
                name: "CoupletIndex",
                table: "DivanVerses");
        }
    }
}
