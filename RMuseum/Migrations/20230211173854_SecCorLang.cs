using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class SecCorLang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "DivanPoemSectionCorrections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LanguageReviewResult",
                table: "DivanPoemSectionCorrections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OriginalLanguage",
                table: "DivanPoemSectionCorrections",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "DivanPoemSectionCorrections");

            migrationBuilder.DropColumn(
                name: "LanguageReviewResult",
                table: "DivanPoemSectionCorrections");

            migrationBuilder.DropColumn(
                name: "OriginalLanguage",
                table: "DivanPoemSectionCorrections");
        }
    }
}
