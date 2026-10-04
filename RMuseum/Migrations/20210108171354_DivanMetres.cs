using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class DivanMetres : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DivanMetreId",
                table: "DivanPoems",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DivanMetres",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UrlSlug = table.Column<string>(nullable: true),
                    Rhythm = table.Column<string>(nullable: true),
                    Name = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanMetres", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoems_DivanMetreId",
                table: "DivanPoems",
                column: "DivanMetreId");

            migrationBuilder.AddForeignKey(
                name: "FK_DivanPoems_DivanMetres_DivanMetreId",
                table: "DivanPoems",
                column: "DivanMetreId",
                principalTable: "DivanMetres",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DivanPoems_DivanMetres_DivanMetreId",
                table: "DivanPoems");

            migrationBuilder.DropTable(
                name: "DivanMetres");

            migrationBuilder.DropIndex(
                name: "IX_DivanPoems_DivanMetreId",
                table: "DivanPoems");

            migrationBuilder.DropColumn(
                name: "DivanMetreId",
                table: "DivanPoems");
        }
    }
}
