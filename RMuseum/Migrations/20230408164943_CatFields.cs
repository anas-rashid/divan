using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class CatFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BookName",
                table: "DivanCategories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MapName",
                table: "DivanCategories",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RImageId",
                table: "DivanCategories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SumUpSubsGeoLocations",
                table: "DivanCategories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_DivanCategories_RImageId",
                table: "DivanCategories",
                column: "RImageId");

            migrationBuilder.AddForeignKey(
                name: "FK_DivanCategories_GeneralImages_RImageId",
                table: "DivanCategories",
                column: "RImageId",
                principalTable: "GeneralImages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DivanCategories_GeneralImages_RImageId",
                table: "DivanCategories");

            migrationBuilder.DropIndex(
                name: "IX_DivanCategories_RImageId",
                table: "DivanCategories");

            migrationBuilder.DropColumn(
                name: "BookName",
                table: "DivanCategories");

            migrationBuilder.DropColumn(
                name: "MapName",
                table: "DivanCategories");

            migrationBuilder.DropColumn(
                name: "RImageId",
                table: "DivanCategories");

            migrationBuilder.DropColumn(
                name: "SumUpSubsGeoLocations",
                table: "DivanCategories");
        }
    }
}
