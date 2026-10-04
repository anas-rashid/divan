using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class Task373Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoupletSummary",
                table: "DivanVerseVOrderText",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LanguageId",
                table: "DivanVerseVOrderText",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LanguageReviewResult",
                table: "DivanVerseVOrderText",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OriginalCoupletSummary",
                table: "DivanVerseVOrderText",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OriginalLanguageId",
                table: "DivanVerseVOrderText",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SummaryReviewResult",
                table: "DivanVerseVOrderText",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CoupletSummary",
                table: "DivanVerses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LanguageId",
                table: "DivanVerses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HideMyName",
                table: "DivanPoemSectionCorrections",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OriginalPoemFormat",
                table: "DivanPoemSectionCorrections",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PoemFormat",
                table: "DivanPoemSectionCorrections",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PoemFormatReviewResult",
                table: "DivanPoemSectionCorrections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PoemSummary",
                table: "DivanPoems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HideMyName",
                table: "DivanPoemCorrections",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OriginalPoemSummary",
                table: "DivanPoemCorrections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PoemSummary",
                table: "DivanPoemCorrections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SummaryReviewResult",
                table: "DivanPoemCorrections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_DivanVerses_LanguageId",
                table: "DivanVerses",
                column: "LanguageId");

            migrationBuilder.AddForeignKey(
                name: "FK_DivanVerses_DivanLanguages_LanguageId",
                table: "DivanVerses",
                column: "LanguageId",
                principalTable: "DivanLanguages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DivanVerses_DivanLanguages_LanguageId",
                table: "DivanVerses");

            migrationBuilder.DropIndex(
                name: "IX_DivanVerses_LanguageId",
                table: "DivanVerses");

            migrationBuilder.DropColumn(
                name: "CoupletSummary",
                table: "DivanVerseVOrderText");

            migrationBuilder.DropColumn(
                name: "LanguageId",
                table: "DivanVerseVOrderText");

            migrationBuilder.DropColumn(
                name: "LanguageReviewResult",
                table: "DivanVerseVOrderText");

            migrationBuilder.DropColumn(
                name: "OriginalCoupletSummary",
                table: "DivanVerseVOrderText");

            migrationBuilder.DropColumn(
                name: "OriginalLanguageId",
                table: "DivanVerseVOrderText");

            migrationBuilder.DropColumn(
                name: "SummaryReviewResult",
                table: "DivanVerseVOrderText");

            migrationBuilder.DropColumn(
                name: "CoupletSummary",
                table: "DivanVerses");

            migrationBuilder.DropColumn(
                name: "LanguageId",
                table: "DivanVerses");

            migrationBuilder.DropColumn(
                name: "HideMyName",
                table: "DivanPoemSectionCorrections");

            migrationBuilder.DropColumn(
                name: "OriginalPoemFormat",
                table: "DivanPoemSectionCorrections");

            migrationBuilder.DropColumn(
                name: "PoemFormat",
                table: "DivanPoemSectionCorrections");

            migrationBuilder.DropColumn(
                name: "PoemFormatReviewResult",
                table: "DivanPoemSectionCorrections");

            migrationBuilder.DropColumn(
                name: "PoemSummary",
                table: "DivanPoems");

            migrationBuilder.DropColumn(
                name: "HideMyName",
                table: "DivanPoemCorrections");

            migrationBuilder.DropColumn(
                name: "OriginalPoemSummary",
                table: "DivanPoemCorrections");

            migrationBuilder.DropColumn(
                name: "PoemSummary",
                table: "DivanPoemCorrections");

            migrationBuilder.DropColumn(
                name: "SummaryReviewResult",
                table: "DivanPoemCorrections");
        }
    }
}
