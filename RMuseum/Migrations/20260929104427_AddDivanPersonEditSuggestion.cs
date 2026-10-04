using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class AddDivanPersonEditSuggestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DivanPersonEditSuggestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SuggestedName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SuggestedDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SuggestedWikiUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SuggestedBirthYearInLHijri = table.Column<int>(type: "int", nullable: false),
                    SuggestedDeathYearInLHijri = table.Column<int>(type: "int", nullable: false),
                    SuggestedValidBirthDate = table.Column<bool>(type: "bit", nullable: false),
                    SuggestedValidDeathDate = table.Column<bool>(type: "bit", nullable: false),
                    SuggestedBirthLocationId = table.Column<int>(type: "int", nullable: true),
                    SuggestedDeathLocationId = table.Column<int>(type: "int", nullable: true),
                    SuggestedFamilyTreeCaption = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SuggestionNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reviewed = table.Column<bool>(type: "bit", nullable: false),
                    Result = table.Column<int>(type: "int", nullable: false),
                    ReviewDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanPersonEditSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanPersonEditSuggestions_AspNetUsers_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DivanPersonEditSuggestions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DivanPersonEditSuggestions_DivanGeoLocations_SuggestedBirthLocationId",
                        column: x => x.SuggestedBirthLocationId,
                        principalTable: "DivanGeoLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DivanPersonEditSuggestions_DivanGeoLocations_SuggestedDeathLocationId",
                        column: x => x.SuggestedDeathLocationId,
                        principalTable: "DivanGeoLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DivanPersonEditSuggestions_DivanRelatedPersons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "DivanRelatedPersons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DivanPersonEditSuggestions_PersonId",
                table: "DivanPersonEditSuggestions",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPersonEditSuggestions_ReviewerUserId",
                table: "DivanPersonEditSuggestions",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPersonEditSuggestions_SuggestedBirthLocationId",
                table: "DivanPersonEditSuggestions",
                column: "SuggestedBirthLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPersonEditSuggestions_SuggestedDeathLocationId",
                table: "DivanPersonEditSuggestions",
                column: "SuggestedDeathLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPersonEditSuggestions_UserId",
                table: "DivanPersonEditSuggestions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DivanPersonEditSuggestions");
        }
    }
}
