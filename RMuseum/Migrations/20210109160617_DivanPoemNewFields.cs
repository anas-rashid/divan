using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class DivanPoemNewFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RhymeLetters",
                table: "DivanPoems",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceName",
                table: "DivanPoems",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrlSlug",
                table: "DivanPoems",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VerseCount",
                table: "DivanMetres",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RhymeLetters",
                table: "DivanPoems");

            migrationBuilder.DropColumn(
                name: "SourceName",
                table: "DivanPoems");

            migrationBuilder.DropColumn(
                name: "SourceUrlSlug",
                table: "DivanPoems");

            migrationBuilder.DropColumn(
                name: "VerseCount",
                table: "DivanMetres");
        }
    }
}
