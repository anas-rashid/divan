using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class NewVerseSectionLang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NewVerse",
                table: "DivanVerseVOrderText",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "NewVerseResult",
                table: "DivanVerseVOrderText",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "DivanPoemSections",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NewVerse",
                table: "DivanVerseVOrderText");

            migrationBuilder.DropColumn(
                name: "NewVerseResult",
                table: "DivanVerseVOrderText");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "DivanPoemSections");
        }
    }
}
