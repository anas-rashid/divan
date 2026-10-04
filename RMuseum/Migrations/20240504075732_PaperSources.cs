using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class PaperSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HumanReviewed",
                table: "PinterestLinks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SuggestedByMachine",
                table: "PDFDivanLinks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DivanPaperSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DivanPoetId = table.Column<int>(type: "int", nullable: false),
                    DivanCatId = table.Column<int>(type: "int", nullable: false),
                    DivanCatFullTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DivanCatFullUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BookType = table.Column<int>(type: "int", nullable: false),
                    BookFullUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NaskbanBookId = table.Column<int>(type: "int", nullable: false),
                    BookFullTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsTextOriginalSource = table.Column<bool>(type: "bit", nullable: false),
                    CoverThumbnailImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MatchPercent = table.Column<int>(type: "int", nullable: false),
                    HumanReviewed = table.Column<bool>(type: "bit", nullable: false),
                    OrderIndicator = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanPaperSources", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DivanPaperSources");

            migrationBuilder.DropColumn(
                name: "HumanReviewed",
                table: "PinterestLinks");

            migrationBuilder.DropColumn(
                name: "SuggestedByMachine",
                table: "PDFDivanLinks");
        }
    }
}
