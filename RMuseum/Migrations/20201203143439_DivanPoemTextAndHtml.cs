using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class DivanPoemTextAndHtml : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HtmlText",
                table: "DivanPoems",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlainText",
                table: "DivanPoems",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HtmlText",
                table: "DivanPoems");

            migrationBuilder.DropColumn(
                name: "PlainText",
                table: "DivanPoems");
        }
    }
}
