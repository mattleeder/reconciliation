using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reconciliation.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRunStatusAndInputs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedUtc",
                table: "Runs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "Runs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Runs",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Complete");

            migrationBuilder.CreateTable(
                name: "RunInputs",
                columns: table => new
                {
                    RunId = table.Column<int>(type: "int", nullable: false),
                    BankCsv = table.Column<string>(type: "nvarchar(max)", maxLength: 1048576, nullable: false),
                    LedgerCsv = table.Column<string>(type: "nvarchar(max)", maxLength: 1048576, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunInputs", x => x.RunId);
                    table.ForeignKey(
                        name: "FK_RunInputs_Runs_RunId",
                        column: x => x.RunId,
                        principalTable: "Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RunInputs");

            migrationBuilder.DropColumn(
                name: "CompletedUtc",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "Runs");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Runs");
        }
    }
}
