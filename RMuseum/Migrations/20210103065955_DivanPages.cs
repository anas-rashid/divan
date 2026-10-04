using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class DivanPages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DivanPages",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false),
                    DivanPageType = table.Column<int>(nullable: false),
                    Published = table.Column<bool>(nullable: false),
                    PageOrder = table.Column<int>(nullable: false),
                    Title = table.Column<string>(nullable: true),
                    FullTitle = table.Column<string>(nullable: true),
                    UrlSlug = table.Column<string>(nullable: true),
                    FullUrl = table.Column<string>(nullable: true),
                    HtmlText = table.Column<string>(nullable: true),
                    ParentId = table.Column<int>(nullable: true),
                    PoetId = table.Column<int>(nullable: true),
                    CatId = table.Column<int>(nullable: true),
                    PoemId = table.Column<int>(nullable: true),
                    SecondPoetId = table.Column<int>(nullable: true),
                    PostDate = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanPages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanPages_DivanCategories_CatId",
                        column: x => x.CatId,
                        principalTable: "DivanCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DivanPages_DivanPages_ParentId",
                        column: x => x.ParentId,
                        principalTable: "DivanPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DivanPages_DivanPoems_PoemId",
                        column: x => x.PoemId,
                        principalTable: "DivanPoems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DivanPages_DivanPoets_PoetId",
                        column: x => x.PoetId,
                        principalTable: "DivanPoets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DivanPages_DivanPoets_SecondPoetId",
                        column: x => x.SecondPoetId,
                        principalTable: "DivanPoets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DivanPages_CatId",
                table: "DivanPages",
                column: "CatId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPages_ParentId",
                table: "DivanPages",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPages_PoemId",
                table: "DivanPages",
                column: "PoemId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPages_PoetId",
                table: "DivanPages",
                column: "PoetId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPages_SecondPoetId",
                table: "DivanPages",
                column: "SecondPoetId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DivanPages");
        }
    }
}
