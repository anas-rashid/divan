using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class PoetNewFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Nickname",
                table: "DivanPoets",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Published",
                table: "DivanPoets",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "RImageId",
                table: "DivanPoets",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoets_RImageId",
                table: "DivanPoets",
                column: "RImageId");

            migrationBuilder.AddForeignKey(
                name: "FK_DivanPoets_GeneralImages_RImageId",
                table: "DivanPoets",
                column: "RImageId",
                principalTable: "GeneralImages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DivanPoets_GeneralImages_RImageId",
                table: "DivanPoets");

            migrationBuilder.DropIndex(
                name: "IX_DivanPoets_RImageId",
                table: "DivanPoets");

            migrationBuilder.DropColumn(
                name: "Nickname",
                table: "DivanPoets");

            migrationBuilder.DropColumn(
                name: "Published",
                table: "DivanPoets");

            migrationBuilder.DropColumn(
                name: "RImageId",
                table: "DivanPoets");
        }
    }
}
