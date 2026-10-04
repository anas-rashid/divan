using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class AddPoemGeoSuggestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DivanPoemGeoDateTagCorrection",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CoupletIndex = table.Column<int>(type: "int", nullable: true),
                    LocationId = table.Column<int>(type: "int", nullable: true),
                    SuggestedLocationName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SuggestedLatitude = table.Column<double>(type: "float", nullable: true),
                    SuggestedLongitude = table.Column<double>(type: "float", nullable: true),
                    LunarYear = table.Column<int>(type: "int", nullable: true),
                    LunarMonth = table.Column<int>(type: "int", nullable: true),
                    LunarDay = table.Column<int>(type: "int", nullable: true),
                    PersonId = table.Column<int>(type: "int", nullable: true),
                    IgnoreInCategory = table.Column<bool>(type: "bit", nullable: false),
                    SuggestionNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MarkForDelete = table.Column<bool>(type: "bit", nullable: false),
                    ExistingTagId = table.Column<int>(type: "int", nullable: true),
                    Result = table.Column<int>(type: "int", nullable: false),
                    ReviewNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DivanPoemCorrectionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanPoemGeoDateTagCorrection", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanPoemGeoDateTagCorrection_DivanGeoLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "DivanGeoLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DivanPoemGeoDateTagCorrection_DivanPoemCorrections_DivanPoemCorrectionId",
                        column: x => x.DivanPoemCorrectionId,
                        principalTable: "DivanPoemCorrections",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DivanPoemGeoDateTagCorrection_DivanRelatedPersons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "DivanRelatedPersons",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoemGeoDateTagCorrection_DivanPoemCorrectionId",
                table: "DivanPoemGeoDateTagCorrection",
                column: "DivanPoemCorrectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoemGeoDateTagCorrection_LocationId",
                table: "DivanPoemGeoDateTagCorrection",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoemGeoDateTagCorrection_PersonId",
                table: "DivanPoemGeoDateTagCorrection",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DivanPoemGeoDateTagCorrection");
        }
    }
}
