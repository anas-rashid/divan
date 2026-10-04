using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class UserPoemQuotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Rejected",
                table: "DivanQuotedPoems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewDate",
                table: "DivanQuotedPoems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "DivanQuotedPoems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewerUserId",
                table: "DivanQuotedPoems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SuggestedById",
                table: "DivanQuotedPoems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuggestionDate",
                table: "DivanQuotedPoems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DivanQuotedPoems_ReviewerUserId",
                table: "DivanQuotedPoems",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanQuotedPoems_SuggestedById",
                table: "DivanQuotedPoems",
                column: "SuggestedById");

            migrationBuilder.AddForeignKey(
                name: "FK_DivanQuotedPoems_AspNetUsers_ReviewerUserId",
                table: "DivanQuotedPoems",
                column: "ReviewerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DivanQuotedPoems_AspNetUsers_SuggestedById",
                table: "DivanQuotedPoems",
                column: "SuggestedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DivanQuotedPoems_AspNetUsers_ReviewerUserId",
                table: "DivanQuotedPoems");

            migrationBuilder.DropForeignKey(
                name: "FK_DivanQuotedPoems_AspNetUsers_SuggestedById",
                table: "DivanQuotedPoems");

            migrationBuilder.DropIndex(
                name: "IX_DivanQuotedPoems_ReviewerUserId",
                table: "DivanQuotedPoems");

            migrationBuilder.DropIndex(
                name: "IX_DivanQuotedPoems_SuggestedById",
                table: "DivanQuotedPoems");

            migrationBuilder.DropColumn(
                name: "Rejected",
                table: "DivanQuotedPoems");

            migrationBuilder.DropColumn(
                name: "ReviewDate",
                table: "DivanQuotedPoems");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "DivanQuotedPoems");

            migrationBuilder.DropColumn(
                name: "ReviewerUserId",
                table: "DivanQuotedPoems");

            migrationBuilder.DropColumn(
                name: "SuggestedById",
                table: "DivanQuotedPoems");

            migrationBuilder.DropColumn(
                name: "SuggestionDate",
                table: "DivanQuotedPoems");
        }
    }
}
