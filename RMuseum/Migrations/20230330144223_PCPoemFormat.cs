using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class PCPoemFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OriginalPoemFormat",
                table: "DivanPoemCorrections",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PoemFormat",
                table: "DivanPoemCorrections",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PoemFormatReviewResult",
                table: "DivanPoemCorrections",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalPoemFormat",
                table: "DivanPoemCorrections");

            migrationBuilder.DropColumn(
                name: "PoemFormat",
                table: "DivanPoemCorrections");

            migrationBuilder.DropColumn(
                name: "PoemFormatReviewResult",
                table: "DivanPoemCorrections");
        }
    }
}
