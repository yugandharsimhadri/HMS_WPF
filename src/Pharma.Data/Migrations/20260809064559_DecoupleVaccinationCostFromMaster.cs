using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pharma.Data.Migrations
{
    /// <inheritdoc />
    public partial class DecoupleVaccinationCostFromMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Price",
                table: "VaccineMasters");

            migrationBuilder.AddColumn<string>(
                name: "Manufacturer",
                table: "VaccinationRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                table: "VaccinationRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductName",
                table: "VaccinationRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationRecords_ProductId",
                table: "VaccinationRecords",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_VaccinationRecords_Products_ProductId",
                table: "VaccinationRecords",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VaccinationRecords_Products_ProductId",
                table: "VaccinationRecords");

            migrationBuilder.DropIndex(
                name: "IX_VaccinationRecords_ProductId",
                table: "VaccinationRecords");

            migrationBuilder.DropColumn(
                name: "Manufacturer",
                table: "VaccinationRecords");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "VaccinationRecords");

            migrationBuilder.DropColumn(
                name: "ProductName",
                table: "VaccinationRecords");

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "VaccineMasters",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
