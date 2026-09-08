using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharma.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVaccinationBatchLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "VaccinationRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationRecords_BatchId",
                table: "VaccinationRecords",
                column: "BatchId");

            migrationBuilder.AddForeignKey(
                name: "FK_VaccinationRecords_Batches_BatchId",
                table: "VaccinationRecords",
                column: "BatchId",
                principalTable: "Batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VaccinationRecords_Batches_BatchId",
                table: "VaccinationRecords");

            migrationBuilder.DropIndex(
                name: "IX_VaccinationRecords_BatchId",
                table: "VaccinationRecords");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "VaccinationRecords");
        }
    }
}
