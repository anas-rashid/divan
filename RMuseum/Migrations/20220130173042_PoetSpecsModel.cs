using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    public partial class PoetSpecsModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DivanPoetSuggestedSpecLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PoetId = table.Column<int>(type: "int", nullable: false),
                    LineOrder = table.Column<int>(type: "int", nullable: false),
                    Contents = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Published = table.Column<bool>(type: "bit", nullable: false),
                    SuggestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanPoetSuggestedSpecLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanPoetSuggestedSpecLines_AspNetUsers_SuggestedById",
                        column: x => x.SuggestedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DivanPoetSuggestedSpecLines_DivanPoets_PoetId",
                        column: x => x.PoetId,
                        principalTable: "DivanPoets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoetSuggestedSpecLines_PoetId",
                table: "DivanPoetSuggestedSpecLines",
                column: "PoetId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoetSuggestedSpecLines_SuggestedById",
                table: "DivanPoetSuggestedSpecLines",
                column: "SuggestedById");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DivanPoetSuggestedSpecLines");
        }
    }
}
