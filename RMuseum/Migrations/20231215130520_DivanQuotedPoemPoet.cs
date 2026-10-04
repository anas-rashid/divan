using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class DivanQuotedPoemPoet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PoetId",
                table: "DivanQuotedPoems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RelatedPoetId",
                table: "DivanQuotedPoems",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PoetId",
                table: "DivanQuotedPoems");

            migrationBuilder.DropColumn(
                name: "RelatedPoetId",
                table: "DivanQuotedPoems");
        }
    }
}
