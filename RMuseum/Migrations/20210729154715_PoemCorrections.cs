using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class PoemCorrections : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DivanPoemCorrections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PoemId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Rhythm = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalRhythm = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reviewed = table.Column<bool>(type: "bit", nullable: false),
                    ReviewDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApplicationOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanPoemCorrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanPoemCorrections_AspNetUsers_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DivanPoemCorrections_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DivanPoemCorrections_DivanPoems_PoemId",
                        column: x => x.PoemId,
                        principalTable: "DivanPoems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DivanVerseVOrderText",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VORder = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Result = table.Column<int>(type: "int", nullable: false),
                    ReviewNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DivanPoemCorrectionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanVerseVOrderText", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanVerseVOrderText_DivanPoemCorrections_DivanPoemCorrectionId",
                        column: x => x.DivanPoemCorrectionId,
                        principalTable: "DivanPoemCorrections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoemCorrections_PoemId",
                table: "DivanPoemCorrections",
                column: "PoemId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoemCorrections_ReviewerUserId",
                table: "DivanPoemCorrections",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoemCorrections_UserId",
                table: "DivanPoemCorrections",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanVerseVOrderText_DivanPoemCorrectionId",
                table: "DivanVerseVOrderText",
                column: "DivanPoemCorrectionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DivanVerseVOrderText");

            migrationBuilder.DropTable(
                name: "DivanPoemCorrections");
        }
    }
}
