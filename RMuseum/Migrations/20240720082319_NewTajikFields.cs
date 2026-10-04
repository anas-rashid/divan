using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class NewTajikFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TajikDescription",
                table: "DivanPoets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TajikNickName",
                table: "DivanPoets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TajikTitle",
                table: "DivanPoems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TajikDescription",
                table: "DivanCategories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TajikTitle",
                table: "DivanCategories",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TajikDescription",
                table: "DivanPoets");

            migrationBuilder.DropColumn(
                name: "TajikNickName",
                table: "DivanPoets");

            migrationBuilder.DropColumn(
                name: "TajikTitle",
                table: "DivanPoems");

            migrationBuilder.DropColumn(
                name: "TajikDescription",
                table: "DivanCategories");

            migrationBuilder.DropColumn(
                name: "TajikTitle",
                table: "DivanCategories");
        }
    }
}
