using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reconciliation.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReconciliationRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RunId",
                table: "Transactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Runs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BankFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    LedgerFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Runs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_RunId",
                table: "Transactions",
                column: "RunId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Runs_RunId",
                table: "Transactions",
                column: "RunId",
                principalTable: "Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Runs_RunId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "Runs");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_RunId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RunId",
                table: "Transactions");
        }
    }
}
