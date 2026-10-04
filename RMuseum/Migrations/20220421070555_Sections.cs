using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    public partial class Sections : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SectionIndex1",
                table: "DivanVerses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SectionIndex2",
                table: "DivanVerses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SectionIndex3",
                table: "DivanVerses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SectionIndex4",
                table: "DivanVerses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalRhythm2",
                table: "DivanPoemCorrections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalRhythm3",
                table: "DivanPoemCorrections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalRhythm4",
                table: "DivanPoemCorrections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rhythm2",
                table: "DivanPoemCorrections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Rhythm2Result",
                table: "DivanPoemCorrections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Rhythm3",
                table: "DivanPoemCorrections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Rhythm3Result",
                table: "DivanPoemCorrections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Rhythm4",
                table: "DivanPoemCorrections",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Rhythm4Result",
                table: "DivanPoemCorrections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DivanCachedRelatedSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PoemId = table.Column<int>(type: "int", nullable: false),
                    SectionIndex = table.Column<int>(type: "int", nullable: false),
                    PoetId = table.Column<int>(type: "int", nullable: false),
                    RelationOrder = table.Column<int>(type: "int", nullable: false),
                    PoetName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PoetImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FullUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FullTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HtmlExcerpt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TargetPoemId = table.Column<int>(type: "int", nullable: false),
                    TargetSectionIndex = table.Column<int>(type: "int", nullable: false),
                    PoetMorePoemsLikeThisCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanCachedRelatedSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanCachedRelatedSections_DivanPoems_PoemId",
                        column: x => x.PoemId,
                        principalTable: "DivanPoems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DivanPoemSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PoemId = table.Column<int>(type: "int", nullable: false),
                    PoetId = table.Column<int>(type: "int", nullable: true),
                    Index = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    SectionType = table.Column<int>(type: "int", nullable: false),
                    VerseType = table.Column<int>(type: "int", nullable: false),
                    DivanMetreId = table.Column<int>(type: "int", nullable: true),
                    DivanMetreRefSectionIndex = table.Column<int>(type: "int", nullable: true),
                    RhymeLetters = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    PlainText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HtmlText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PoemFormat = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanPoemSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanPoemSections_DivanMetres_DivanMetreId",
                        column: x => x.DivanMetreId,
                        principalTable: "DivanMetres",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DivanPoemSections_DivanPoems_PoemId",
                        column: x => x.PoemId,
                        principalTable: "DivanPoems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DivanPoemSections_DivanPoets_PoetId",
                        column: x => x.PoetId,
                        principalTable: "DivanPoets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DivanCachedRelatedSections_PoemId_SectionIndex",
                table: "DivanCachedRelatedSections",
                columns: new[] { "PoemId", "SectionIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoemSections_DivanMetreId_RhymeLetters",
                table: "DivanPoemSections",
                columns: new[] { "DivanMetreId", "RhymeLetters" });

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoemSections_DivanMetreId_RhymeLetters_Id",
                table: "DivanPoemSections",
                columns: new[] { "DivanMetreId", "RhymeLetters", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoemSections_PoemId_Index",
                table: "DivanPoemSections",
                columns: new[] { "PoemId", "Index" });

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoemSections_PoetId",
                table: "DivanPoemSections",
                column: "PoetId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoemSections_RhymeLetters",
                table: "DivanPoemSections",
                column: "RhymeLetters");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DivanCachedRelatedSections");

            migrationBuilder.DropTable(
                name: "DivanPoemSections");

            migrationBuilder.DropColumn(
                name: "SectionIndex1",
                table: "DivanVerses");

            migrationBuilder.DropColumn(
                name: "SectionIndex2",
                table: "DivanVerses");

            migrationBuilder.DropColumn(
                name: "SectionIndex3",
                table: "DivanVerses");

            migrationBuilder.DropColumn(
                name: "SectionIndex4",
                table: "DivanVerses");

            migrationBuilder.DropColumn(
                name: "OriginalRhythm2",
                table: "DivanPoemCorrections");

            migrationBuilder.DropColumn(
                name: "OriginalRhythm3",
                table: "DivanPoemCorrections");

            migrationBuilder.DropColumn(
                name: "OriginalRhythm4",
                table: "DivanPoemCorrections");

            migrationBuilder.DropColumn(
                name: "Rhythm2",
                table: "DivanPoemCorrections");

            migrationBuilder.DropColumn(
                name: "Rhythm2Result",
                table: "DivanPoemCorrections");

            migrationBuilder.DropColumn(
                name: "Rhythm3",
                table: "DivanPoemCorrections");

            migrationBuilder.DropColumn(
                name: "Rhythm3Result",
                table: "DivanPoemCorrections");

            migrationBuilder.DropColumn(
                name: "Rhythm4",
                table: "DivanPoemCorrections");

            migrationBuilder.DropColumn(
                name: "Rhythm4Result",
                table: "DivanPoemCorrections");
        }
    }
}
