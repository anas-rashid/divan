using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class SingerImage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RImageId",
                table: "DivanSingers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DivanSingers_RImageId",
                table: "DivanSingers",
                column: "RImageId");

            migrationBuilder.AddForeignKey(
                name: "FK_DivanSingers_GeneralImages_RImageId",
                table: "DivanSingers",
                column: "RImageId",
                principalTable: "GeneralImages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DivanSingers_GeneralImages_RImageId",
                table: "DivanSingers");

            migrationBuilder.DropIndex(
                name: "IX_DivanSingers_RImageId",
                table: "DivanSingers");

            migrationBuilder.DropColumn(
                name: "RImageId",
                table: "DivanSingers");
        }
    }
}
