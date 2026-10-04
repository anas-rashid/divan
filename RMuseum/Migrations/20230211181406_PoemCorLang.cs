using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class PoemCorLang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "DivanPoemCorrections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LanguageReviewResult",
                table: "DivanPoemCorrections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OriginalLanguage",
                table: "DivanPoemCorrections",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "DivanPoemCorrections");

            migrationBuilder.DropColumn(
                name: "LanguageReviewResult",
                table: "DivanPoemCorrections");

            migrationBuilder.DropColumn(
                name: "OriginalLanguage",
                table: "DivanPoemCorrections");
        }
    }
}
