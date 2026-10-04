using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    public partial class DivanPoemSectionNewIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DivanPoemSections_DivanMetreId_RhymeLetters_SectionType",
                table: "DivanPoemSections",
                columns: new[] { "DivanMetreId", "RhymeLetters", "SectionType" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DivanPoemSections_DivanMetreId_RhymeLetters_SectionType",
                table: "DivanPoemSections");
        }
    }
}
