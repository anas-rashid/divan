using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class DivanCenturies : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BirthYearInLHijri",
                table: "DivanPoets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DeathYearInLHijri",
                table: "DivanPoets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PinOrder",
                table: "DivanPoets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DivanCenturies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HalfCenturyOrder = table.Column<int>(type: "int", nullable: false),
                    StartYear = table.Column<int>(type: "int", nullable: false),
                    EndYear = table.Column<int>(type: "int", nullable: false),
                    ShowInTimeLine = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanCenturies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DivanCenturyPoet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PoetOrder = table.Column<int>(type: "int", nullable: false),
                    PoetId = table.Column<int>(type: "int", nullable: true),
                    DivanCenturyId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanCenturyPoet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanCenturyPoet_DivanCenturies_DivanCenturyId",
                        column: x => x.DivanCenturyId,
                        principalTable: "DivanCenturies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DivanCenturyPoet_DivanPoets_PoetId",
                        column: x => x.PoetId,
                        principalTable: "DivanPoets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DivanCenturyPoet_DivanCenturyId",
                table: "DivanCenturyPoet",
                column: "DivanCenturyId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanCenturyPoet_PoetId",
                table: "DivanCenturyPoet",
                column: "PoetId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DivanCenturyPoet");

            migrationBuilder.DropTable(
                name: "DivanCenturies");

            migrationBuilder.DropColumn(
                name: "BirthYearInLHijri",
                table: "DivanPoets");

            migrationBuilder.DropColumn(
                name: "DeathYearInLHijri",
                table: "DivanPoets");

            migrationBuilder.DropColumn(
                name: "PinOrder",
                table: "DivanPoets");
        }
    }
}
