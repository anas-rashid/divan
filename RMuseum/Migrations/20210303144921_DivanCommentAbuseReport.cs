using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class DivanCommentAbuseReport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DivanReportedComments",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DivanCommentId = table.Column<int>(nullable: false),
                    ReasonCode = table.Column<string>(nullable: true),
                    ReasonText = table.Column<string>(nullable: true),
                    ReportedById = table.Column<Guid>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanReportedComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanReportedComments_DivanComments_DivanCommentId",
                        column: x => x.DivanCommentId,
                        principalTable: "DivanComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DivanReportedComments_AspNetUsers_ReportedById",
                        column: x => x.ReportedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DivanReportedComments_DivanCommentId",
                table: "DivanReportedComments",
                column: "DivanCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_DivanReportedComments_ReportedById",
                table: "DivanReportedComments",
                column: "ReportedById");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DivanReportedComments");
        }
    }
}
