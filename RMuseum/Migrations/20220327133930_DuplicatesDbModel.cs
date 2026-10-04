using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    public partial class DuplicatesDbModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DivanDuplicates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SrcCatId = table.Column<int>(type: "int", nullable: false),
                    SrcPoemId = table.Column<int>(type: "int", nullable: false),
                    DestPoemId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanDuplicates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanDuplicates_DivanPoems_DestPoemId",
                        column: x => x.DestPoemId,
                        principalTable: "DivanPoems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DivanDuplicates_DivanPoems_SrcPoemId",
                        column: x => x.SrcPoemId,
                        principalTable: "DivanPoems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DivanDuplicates_DestPoemId",
                table: "DivanDuplicates",
                column: "DestPoemId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanDuplicates_SrcPoemId",
                table: "DivanDuplicates",
                column: "SrcPoemId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DivanDuplicates");
        }
    }
}
