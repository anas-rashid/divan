using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class DivanGeoLocation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BirthLocationId",
                table: "DivanPoets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeathLocationId",
                table: "DivanPoets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ValidBirthDate",
                table: "DivanPoets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ValidDeathDate",
                table: "DivanPoets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DivanGeoLocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(12,9)", nullable: false),
                    Longitude = table.Column<decimal>(type: "decimal(12,9)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanGeoLocations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoets_BirthLocationId",
                table: "DivanPoets",
                column: "BirthLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoets_DeathLocationId",
                table: "DivanPoets",
                column: "DeathLocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_DivanPoets_DivanGeoLocations_BirthLocationId",
                table: "DivanPoets",
                column: "BirthLocationId",
                principalTable: "DivanGeoLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DivanPoets_DivanGeoLocations_DeathLocationId",
                table: "DivanPoets",
                column: "DeathLocationId",
                principalTable: "DivanGeoLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DivanPoets_DivanGeoLocations_BirthLocationId",
                table: "DivanPoets");

            migrationBuilder.DropForeignKey(
                name: "FK_DivanPoets_DivanGeoLocations_DeathLocationId",
                table: "DivanPoets");

            migrationBuilder.DropTable(
                name: "DivanGeoLocations");

            migrationBuilder.DropIndex(
                name: "IX_DivanPoets_BirthLocationId",
                table: "DivanPoets");

            migrationBuilder.DropIndex(
                name: "IX_DivanPoets_DeathLocationId",
                table: "DivanPoets");

            migrationBuilder.DropColumn(
                name: "BirthLocationId",
                table: "DivanPoets");

            migrationBuilder.DropColumn(
                name: "DeathLocationId",
                table: "DivanPoets");

            migrationBuilder.DropColumn(
                name: "ValidBirthDate",
                table: "DivanPoets");

            migrationBuilder.DropColumn(
                name: "ValidDeathDate",
                table: "DivanPoets");
        }
    }
}
