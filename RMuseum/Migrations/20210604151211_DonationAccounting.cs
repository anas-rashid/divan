using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class DonationAccounting : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DivanDonations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DateString = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecordDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AmountString = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DonorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DonorLink = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Remaining = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpenditureDesc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImportedRecord = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanDonations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DivanExpenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpenseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanExpenses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DonationExpenditure",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DivanDonationId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DivanExpenseId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonationExpenditure", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DonationExpenditure_DivanDonations_DivanDonationId",
                        column: x => x.DivanDonationId,
                        principalTable: "DivanDonations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DonationExpenditure_DivanExpenses_DivanExpenseId",
                        column: x => x.DivanExpenseId,
                        principalTable: "DivanExpenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DonationExpenditure_DivanDonationId",
                table: "DonationExpenditure",
                column: "DivanDonationId");

            migrationBuilder.CreateIndex(
                name: "IX_DonationExpenditure_DivanExpenseId",
                table: "DonationExpenditure",
                column: "DivanExpenseId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DonationExpenditure");

            migrationBuilder.DropTable(
                name: "DivanDonations");

            migrationBuilder.DropTable(
                name: "DivanExpenses");
        }
    }
}
