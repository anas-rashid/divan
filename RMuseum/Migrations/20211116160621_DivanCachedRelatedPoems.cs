using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class DivanCachedRelatedPoems : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DivanCachedRelatedPoems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PoemId = table.Column<int>(type: "int", nullable: false),
                    PoetId = table.Column<int>(type: "int", nullable: false),
                    RelationOrder = table.Column<int>(type: "int", nullable: false),
                    PoetName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PoetImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FullUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FullTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HtmlExcerpt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PoetMorePoemsLikeThisCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanCachedRelatedPoems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanCachedRelatedPoems_DivanPoems_PoemId",
                        column: x => x.PoemId,
                        principalTable: "DivanPoems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DivanCachedRelatedPoems_PoemId",
                table: "DivanCachedRelatedPoems",
                column: "PoemId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DivanCachedRelatedPoems");
        }
    }
}
