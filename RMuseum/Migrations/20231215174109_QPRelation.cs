using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class QPRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DivanQuotedPoems_PoemId",
                table: "DivanQuotedPoems",
                column: "PoemId");

            migrationBuilder.AddForeignKey(
                name: "FK_DivanQuotedPoems_DivanPoems_PoemId",
                table: "DivanQuotedPoems",
                column: "PoemId",
                principalTable: "DivanPoems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DivanQuotedPoems_DivanPoems_PoemId",
                table: "DivanQuotedPoems");

            migrationBuilder.DropIndex(
                name: "IX_DivanQuotedPoems_PoemId",
                table: "DivanQuotedPoems");
        }
    }
}
