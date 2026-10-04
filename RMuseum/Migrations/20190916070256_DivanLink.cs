using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class DivanLink : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DivanLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    DivanPostId = table.Column<int>(nullable: false),
                    DivanUrl = table.Column<string>(nullable: true),
                    DivanTitle = table.Column<string>(nullable: true),
                    ArtifactId = table.Column<Guid>(nullable: false),
                    ItemId = table.Column<Guid>(nullable: true),
                    SuggestedById = table.Column<Guid>(nullable: false),
                    SuggestionDate = table.Column<DateTime>(nullable: false),
                    ReviewerId = table.Column<Guid>(nullable: true),
                    ReviewDate = table.Column<DateTime>(nullable: false),
                    ReviewResult = table.Column<int>(nullable: false),
                    Synchronized = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanLinks_Artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalTable: "Artifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DivanLinks_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DivanLinks_AspNetUsers_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DivanLinks_AspNetUsers_SuggestedById",
                        column: x => x.SuggestedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DivanLinks_ArtifactId",
                table: "DivanLinks",
                column: "ArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanLinks_ItemId",
                table: "DivanLinks",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanLinks_ReviewerId",
                table: "DivanLinks",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanLinks_SuggestedById",
                table: "DivanLinks",
                column: "SuggestedById");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DivanLinks");
        }
    }
}
