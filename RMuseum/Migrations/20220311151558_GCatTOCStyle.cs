using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    public partial class GCatTOCStyle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "DivanPoems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MixedModeOrder",
                table: "DivanPoems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Published",
                table: "DivanPoems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NoIndex",
                table: "DivanPages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RedirectFromFullUrl",
                table: "DivanPages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CatType",
                table: "DivanCategories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "DivanCategories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionHtml",
                table: "DivanCategories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MixedModeOrder",
                table: "DivanCategories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Published",
                table: "DivanCategories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TableOfContentsStyle",
                table: "DivanCategories",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "DivanPoems");

            migrationBuilder.DropColumn(
                name: "MixedModeOrder",
                table: "DivanPoems");

            migrationBuilder.DropColumn(
                name: "Published",
                table: "DivanPoems");

            migrationBuilder.DropColumn(
                name: "NoIndex",
                table: "DivanPages");

            migrationBuilder.DropColumn(
                name: "RedirectFromFullUrl",
                table: "DivanPages");

            migrationBuilder.DropColumn(
                name: "CatType",
                table: "DivanCategories");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "DivanCategories");

            migrationBuilder.DropColumn(
                name: "DescriptionHtml",
                table: "DivanCategories");

            migrationBuilder.DropColumn(
                name: "MixedModeOrder",
                table: "DivanCategories");

            migrationBuilder.DropColumn(
                name: "Published",
                table: "DivanCategories");

            migrationBuilder.DropColumn(
                name: "TableOfContentsStyle",
                table: "DivanCategories");
        }
    }
}
