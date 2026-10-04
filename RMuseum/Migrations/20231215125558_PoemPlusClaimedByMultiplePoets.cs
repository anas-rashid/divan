using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class PoemPlusClaimedByMultiplePoets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ClaimedByBothPoets",
                table: "DivanQuotedPoems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ClaimedByMultiplePoets",
                table: "DivanPoems",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClaimedByBothPoets",
                table: "DivanQuotedPoems");

            migrationBuilder.DropColumn(
                name: "ClaimedByMultiplePoets",
                table: "DivanPoems");
        }
    }
}
