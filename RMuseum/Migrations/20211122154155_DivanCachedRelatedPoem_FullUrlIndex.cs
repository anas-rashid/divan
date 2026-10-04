using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    public partial class DivanCachedRelatedPoem_FullUrlIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FullUrl",
                table: "DivanCachedRelatedPoems",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DivanCachedRelatedPoems_FullUrl",
                table: "DivanCachedRelatedPoems",
                column: "FullUrl");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DivanCachedRelatedPoems_FullUrl",
                table: "DivanCachedRelatedPoems");

            migrationBuilder.AlterColumn<string>(
                name: "FullUrl",
                table: "DivanCachedRelatedPoems",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);
        }
    }
}
