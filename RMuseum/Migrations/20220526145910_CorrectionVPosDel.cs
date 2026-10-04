using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    public partial class CorrectionVPosDel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MarkForDelete",
                table: "DivanVerseVOrderText",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MarkForDeleteResult",
                table: "DivanVerseVOrderText",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OriginalVersePosition",
                table: "DivanVerseVOrderText",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VersePosition",
                table: "DivanVerseVOrderText",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VersePositionResult",
                table: "DivanVerseVOrderText",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarkForDelete",
                table: "DivanVerseVOrderText");

            migrationBuilder.DropColumn(
                name: "MarkForDeleteResult",
                table: "DivanVerseVOrderText");

            migrationBuilder.DropColumn(
                name: "OriginalVersePosition",
                table: "DivanVerseVOrderText");

            migrationBuilder.DropColumn(
                name: "VersePosition",
                table: "DivanVerseVOrderText");

            migrationBuilder.DropColumn(
                name: "VersePositionResult",
                table: "DivanVerseVOrderText");
        }
    }
}
