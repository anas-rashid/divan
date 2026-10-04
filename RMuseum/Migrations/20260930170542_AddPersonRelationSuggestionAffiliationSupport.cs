using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonRelationSuggestionAffiliationSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExistingAffiliationId",
                table: "DivanPersonRelationEditSuggestions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "DivanPersonRelationEditSuggestions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SuggestedAffiliationType",
                table: "DivanPersonRelationEditSuggestions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DivanPersonRelationEditSuggestions_ExistingAffiliationId",
                table: "DivanPersonRelationEditSuggestions",
                column: "ExistingAffiliationId");

            migrationBuilder.AddForeignKey(
                name: "FK_DivanPersonRelationEditSuggestions_DivanPersonAffiliations_ExistingAffiliationId",
                table: "DivanPersonRelationEditSuggestions",
                column: "ExistingAffiliationId",
                principalTable: "DivanPersonAffiliations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DivanPersonRelationEditSuggestions_DivanPersonAffiliations_ExistingAffiliationId",
                table: "DivanPersonRelationEditSuggestions");

            migrationBuilder.DropIndex(
                name: "IX_DivanPersonRelationEditSuggestions_ExistingAffiliationId",
                table: "DivanPersonRelationEditSuggestions");

            migrationBuilder.DropColumn(
                name: "ExistingAffiliationId",
                table: "DivanPersonRelationEditSuggestions");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "DivanPersonRelationEditSuggestions");

            migrationBuilder.DropColumn(
                name: "SuggestedAffiliationType",
                table: "DivanPersonRelationEditSuggestions");
        }
    }
}
