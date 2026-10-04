using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class FamilyTreeModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FamilyTreeCaption",
                table: "DivanRelatedPersons",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuggestedPersonGraphJson",
                table: "DivanPoemGeoDateTagCorrection",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DivanPersonAffiliations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Person1Id = table.Column<int>(type: "int", nullable: false),
                    Person2Id = table.Column<int>(type: "int", nullable: false),
                    AffiliationType = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanPersonAffiliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanPersonAffiliations_DivanRelatedPersons_Person1Id",
                        column: x => x.Person1Id,
                        principalTable: "DivanRelatedPersons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DivanPersonAffiliations_DivanRelatedPersons_Person2Id",
                        column: x => x.Person2Id,
                        principalTable: "DivanRelatedPersons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DivanPersonRelations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Person1Id = table.Column<int>(type: "int", nullable: false),
                    Person2Id = table.Column<int>(type: "int", nullable: false),
                    RelationType = table.Column<int>(type: "int", nullable: false),
                    DegreeHint = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanPersonRelations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanPersonRelations_DivanRelatedPersons_Person1Id",
                        column: x => x.Person1Id,
                        principalTable: "DivanRelatedPersons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DivanPersonRelations_DivanRelatedPersons_Person2Id",
                        column: x => x.Person2Id,
                        principalTable: "DivanRelatedPersons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DivanPersonAffiliations_Person1Id",
                table: "DivanPersonAffiliations",
                column: "Person1Id");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPersonAffiliations_Person2Id",
                table: "DivanPersonAffiliations",
                column: "Person2Id");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPersonRelations_Person1Id",
                table: "DivanPersonRelations",
                column: "Person1Id");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPersonRelations_Person2Id",
                table: "DivanPersonRelations",
                column: "Person2Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DivanPersonAffiliations");

            migrationBuilder.DropTable(
                name: "DivanPersonRelations");

            migrationBuilder.DropColumn(
                name: "FamilyTreeCaption",
                table: "DivanRelatedPersons");

            migrationBuilder.DropColumn(
                name: "SuggestedPersonGraphJson",
                table: "DivanPoemGeoDateTagCorrection");
        }
    }
}
