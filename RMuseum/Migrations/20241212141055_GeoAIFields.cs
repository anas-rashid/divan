using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class GeoAIFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MachineGenerated",
                table: "PoemGeoDateTags",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MachineGenerated",
                table: "DivanRelatedPersons",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MachineGenerated",
                table: "DivanGeoLocations",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MachineGenerated",
                table: "PoemGeoDateTags");

            migrationBuilder.DropColumn(
                name: "MachineGenerated",
                table: "DivanRelatedPersons");

            migrationBuilder.DropColumn(
                name: "MachineGenerated",
                table: "DivanGeoLocations");
        }
    }
}
