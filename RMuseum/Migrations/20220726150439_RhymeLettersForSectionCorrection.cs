using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    public partial class RhymeLettersForSectionCorrection : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalRhymeLetters",
                table: "DivanPoemSectionCorrections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RhymeLetters",
                table: "DivanPoemSectionCorrections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RhymeLettersReviewResult",
                table: "DivanPoemSectionCorrections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OriginalRhymeLetters",
                table: "DivanPoemCorrections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RhymeLetters",
                table: "DivanPoemCorrections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RhymeLettersReviewResult",
                table: "DivanPoemCorrections",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalRhymeLetters",
                table: "DivanPoemSectionCorrections");

            migrationBuilder.DropColumn(
                name: "RhymeLetters",
                table: "DivanPoemSectionCorrections");

            migrationBuilder.DropColumn(
                name: "RhymeLettersReviewResult",
                table: "DivanPoemSectionCorrections");

            migrationBuilder.DropColumn(
                name: "OriginalRhymeLetters",
                table: "DivanPoemCorrections");

            migrationBuilder.DropColumn(
                name: "RhymeLetters",
                table: "DivanPoemCorrections");

            migrationBuilder.DropColumn(
                name: "RhymeLettersReviewResult",
                table: "DivanPoemCorrections");
        }
    }
}
