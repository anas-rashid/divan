using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class DivanPoemOldTag : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OldTag",
                table: "DivanPoems",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OldTagPageUrl",
                table: "DivanPoems",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OldTag",
                table: "DivanPoems");

            migrationBuilder.DropColumn(
                name: "OldTagPageUrl",
                table: "DivanPoems");
        }
    }
}
