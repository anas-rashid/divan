using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class DivanPageSnapshot : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DivanPageSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DivanPageId = table.Column<int>(nullable: false),
                    MadeObsoleteByUserId = table.Column<Guid>(nullable: false),
                    RecordDate = table.Column<DateTime>(nullable: false),
                    Note = table.Column<string>(nullable: true),
                    Title = table.Column<string>(nullable: true),
                    UrlSlug = table.Column<string>(nullable: true),
                    HtmlText = table.Column<string>(nullable: true),
                    Rhythm = table.Column<string>(nullable: true),
                    RhymeLetters = table.Column<string>(nullable: true),
                    SourceName = table.Column<string>(nullable: true),
                    SourceUrlSlug = table.Column<string>(nullable: true),
                    OldTag = table.Column<string>(nullable: true),
                    OldTagPageUrl = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanPageSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanPageSnapshots_DivanPages_DivanPageId",
                        column: x => x.DivanPageId,
                        principalTable: "DivanPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DivanPageSnapshots_AspNetUsers_MadeObsoleteByUserId",
                        column: x => x.MadeObsoleteByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DivanPageSnapshots_DivanPageId",
                table: "DivanPageSnapshots",
                column: "DivanPageId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPageSnapshots_MadeObsoleteByUserId",
                table: "DivanPageSnapshots",
                column: "MadeObsoleteByUserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DivanPageSnapshots");
        }
    }
}
