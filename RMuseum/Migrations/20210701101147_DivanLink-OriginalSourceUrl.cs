using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class DivanLinkOriginalSourceUrl : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LinkToOriginalSource",
                table: "DivanLinks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OriginalSourceUrl",
                table: "DivanLinks",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LinkToOriginalSource",
                table: "DivanLinks");

            migrationBuilder.DropColumn(
                name: "OriginalSourceUrl",
                table: "DivanLinks");
        }
    }
}
