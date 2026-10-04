using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class AddDivanPersonRelationEditSuggestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SuggestedForDeletion",
                table: "DivanPersonEditSuggestions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DivanPersonRelationEditSuggestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Action = table.Column<int>(type: "int", nullable: false),
                    ExistingRelationId = table.Column<int>(type: "int", nullable: true),
                    Person1Id = table.Column<int>(type: "int", nullable: false),
                    Person2Id = table.Column<int>(type: "int", nullable: false),
                    SuggestedRelationType = table.Column<int>(type: "int", nullable: false),
                    SuggestedDegreeHint = table.Column<int>(type: "int", nullable: true),
                    SuggestedNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SuggestionNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reviewed = table.Column<bool>(type: "bit", nullable: false),
                    Result = table.Column<int>(type: "int", nullable: false),
                    ReviewDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanPersonRelationEditSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanPersonRelationEditSuggestions_AspNetUsers_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DivanPersonRelationEditSuggestions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DivanPersonRelationEditSuggestions_DivanPersonRelations_ExistingRelationId",
                        column: x => x.ExistingRelationId,
                        principalTable: "DivanPersonRelations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DivanPersonRelationEditSuggestions_DivanRelatedPersons_Person1Id",
                        column: x => x.Person1Id,
                        principalTable: "DivanRelatedPersons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DivanPersonRelationEditSuggestions_DivanRelatedPersons_Person2Id",
                        column: x => x.Person2Id,
                        principalTable: "DivanRelatedPersons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DivanPersonRelationEditSuggestions_ExistingRelationId",
                table: "DivanPersonRelationEditSuggestions",
                column: "ExistingRelationId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPersonRelationEditSuggestions_Person1Id",
                table: "DivanPersonRelationEditSuggestions",
                column: "Person1Id");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPersonRelationEditSuggestions_Person2Id",
                table: "DivanPersonRelationEditSuggestions",
                column: "Person2Id");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPersonRelationEditSuggestions_ReviewerUserId",
                table: "DivanPersonRelationEditSuggestions",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPersonRelationEditSuggestions_UserId",
                table: "DivanPersonRelationEditSuggestions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DivanPersonRelationEditSuggestions");

            migrationBuilder.DropColumn(
                name: "SuggestedForDeletion",
                table: "DivanPersonEditSuggestions");
        }
    }
}
