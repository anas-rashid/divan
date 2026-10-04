using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class RemTrans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DivanVerseTranslation");

            migrationBuilder.DropTable(
                name: "DivanPoemTranslations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DivanPoemTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LanguageId = table.Column<int>(type: "int", nullable: false),
                    PoemId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Published = table.Column<bool>(type: "bit", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanPoemTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanPoemTranslations_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DivanPoemTranslations_DivanLanguages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "DivanLanguages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DivanPoemTranslations_DivanPoems_PoemId",
                        column: x => x.PoemId,
                        principalTable: "DivanPoems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DivanVerseTranslation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VerseId = table.Column<int>(type: "int", nullable: false),
                    DivanPoemTranslationId = table.Column<int>(type: "int", nullable: true),
                    TText = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanVerseTranslation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanVerseTranslation_DivanPoemTranslations_DivanPoemTranslationId",
                        column: x => x.DivanPoemTranslationId,
                        principalTable: "DivanPoemTranslations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DivanVerseTranslation_DivanVerses_VerseId",
                        column: x => x.VerseId,
                        principalTable: "DivanVerses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoemTranslations_LanguageId",
                table: "DivanPoemTranslations",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoemTranslations_PoemId",
                table: "DivanPoemTranslations",
                column: "PoemId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoemTranslations_UserId",
                table: "DivanPoemTranslations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanVerseTranslation_DivanPoemTranslationId",
                table: "DivanVerseTranslation",
                column: "DivanPoemTranslationId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanVerseTranslation_VerseId",
                table: "DivanVerseTranslation",
                column: "VerseId");
        }
    }
}
